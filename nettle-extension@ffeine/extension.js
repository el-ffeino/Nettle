import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import Cogl from 'gi://Cogl';
import Meta from 'gi://Meta';
import Shell from 'gi://Shell';
import Clutter from 'gi://Clutter';
import GObject from 'gi://GObject';

import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import * as Workspace from 'resource:///org/gnome/shell/ui/workspace.js';
import * as WorkspaceThumbnail from 'resource:///org/gnome/shell/ui/workspaceThumbnail.js';
import * as OverviewControls from 'resource:///org/gnome/shell/ui/overviewControls.js';

import {Extension} from 'resource:///org/gnome/shell/extensions/extension.js';

const BUS_NAME = 'io.ffeine.NettleExtension';
const OBJECT_PATH = '/io/ffeine/NettleExtension';
const IFACE_NAME = 'io.ffeine.NettleExtension';

const SETTINGS_SCHEMA = 'org.gnome.shell.extensions.nettle';
const SETTING_ADD_OVERVIEW = 'overview-background';
const SETTING_WORKSPACE_PATCH = 'workspace-patch';

const WorkspacePreviewRadius = 34;
const WorkspacePreviewEdgeSoftness = 1.25; // 1.25 looked fine-ish

const DBUS_XML = `
<node>
  <interface name="${IFACE_NAME}">
    <method name="Attach">
      <arg type="s" name="token" direction="in"/>
      <arg type="i" name="x" direction="in"/>
      <arg type="i" name="y" direction="in"/>
      <arg type="i" name="width" direction="in"/>
      <arg type="i" name="height" direction="in"/>
      <arg type="b" name="ok" direction="out"/>
      <arg type="s" name="message" direction="out"/>
    </method>

    <method name="Detach">
      <arg type="s" name="token" direction="in"/>
      <arg type="b" name="ok" direction="out"/>
      <arg type="s" name="message" direction="out"/>
    </method>

    <method name="DetachAll">
      <arg type="b" name="ok" direction="out"/>
      <arg type="s" name="message" direction="out"/>
    </method>

    <signal name="WindowStateChanged">
        <arg type="s" name="title"/>
        <arg type="s" name="wmClass"/>
        <arg type="b" name="maximized"/>
        <arg type="b" name="fullscreen"/>
        <arg type="b" name="active"/>
        <arg type="i" name="monitorIndex"/>
    </signal>
  </interface>
</node>`;

class WindowFilter {
    constructor() {
        this._hidden = new Set();
        this._originals = [];
    }

    add(metaWindow) {
        if (metaWindow)
            this._hidden.add(metaWindow);
    }

    remove(metaWindow) {
        if (metaWindow)
            this._hidden.delete(metaWindow);
    }

    _override(obj, method, replacement) {
        const original = obj[method];
        this._originals.push([obj, method, original]);
        obj[method] = replacement(original);
    }

    purgeFromExistingWorkspaces(metaWindow) {
        if (!metaWindow)
            return;
    
        const overviewGroup = Main.layoutManager.overviewGroup;
    
        if (!overviewGroup)
            return;
    
        const scan = actor => {
            if (!actor)
                return;
    
            try {
                if (actor instanceof Workspace.Workspace) {
                    if (actor.containsMetaWindow?.(metaWindow)) {
                        try {
                            actor._doRemoveWindow(metaWindow);
                        } catch (e) {
                            console.error(`[NettleExtension] failed to purge MPV from Workspace: ${e}`);
                        }
                    }
                }
            } catch (_e) {
            }
    
            for (const child of actor.get_children?.() ?? [])
                scan(child);
        };
    
        scan(overviewGroup);
    }

    enable() {
        const hidden = this._hidden;

        this._override(global, 'get_window_actors', original => {
            return function () {
                return original.call(this).filter(actor => {
                    const win = actor.get_meta_window?.() ?? actor.meta_window;
                    return !hidden.has(win);
                });
            };
        });

        this._override(Workspace.Workspace.prototype, '_doAddWindow', original => {
            return function (metaWindow) {
                if (hidden.has(metaWindow))
                    return null;
        
                return original.call(this, metaWindow);
            };
        });

        this._override(Meta.Display.prototype, 'get_tab_list', original => {
            return function (type, workspace) {
                return original.call(this, type, workspace)
                    .filter(win => !hidden.has(win));
            };
        });

        this._override(Workspace.Workspace.prototype, '_isOverviewWindow', original => {
            return function (win) {
                if (hidden.has(win))
                    return false;

                return original.call(this, win);
            };
        });

        this._override(WorkspaceThumbnail.WorkspaceThumbnail.prototype, '_isOverviewWindow', original => {
            return function (win) {
                if (hidden.has(win))
                    return false;

                return original.call(this, win);
            };
        });

        this._override(Shell.WindowTracker.prototype, 'get_window_app', original => {
            return function (win) {
                if (hidden.has(win))
                    return null;

                return original.call(this, win);
            };
        });

        this._override(Shell.App.prototype, 'get_windows', original => {
            return function () {
                return original.call(this).filter(win => !hidden.has(win));
            };
        });

        this._override(Shell.AppSystem.prototype, 'get_running', original => {
            return function () {
                return original.call(this)
                    .filter(app => app.get_windows().length > 0);
            };
        });
    }

    disable() {
        for (const [obj, method, original] of this._originals)
            obj[method] = original;

        this._originals = [];
        this._hidden.clear();
    }
}

class OverviewCloneLayer {
    constructor(entries, featureSettings) {
        this._entries = entries;
        this._featureSettings = featureSettings;
        this._layer = null;
        this._signals = [];
    }

    _enabled() {
        return this._featureSettings.overviewBackground;
    }

    enable() {
        if (!this._enabled())
            return;

        this._signals.push([
            Main.overview,
            Main.overview.connect('showing', () => this.rebuild())
        ]);

        this._signals.push([
            Main.overview,
            Main.overview.connect('shown', () => this.rebuild())
        ]);

        this._signals.push([
            Main.overview,
            Main.overview.connect('hidden', () => this.destroyLayer())
        ]);
    }

    disable() {
        this.destroyLayer();

        for (const [obj, id] of this._signals)
            obj.disconnect(id);

        this._signals = [];
    }

    rebuild() {
        if (!this._enabled())
            return;

        const overviewGroup = Main.layoutManager.overviewGroup;

        if (!overviewGroup)
            return;

        this.destroyLayer();

        this._layer = new Clutter.Actor({
            reactive: false,
            visible: true,
        });

        this._layer.set_position(0, 0);
        this._layer.set_size(global.stage.width, global.stage.height);

        // Index 0: behind overview UI.
        overviewGroup.insert_child_at_index(this._layer, 0);

        for (const entry of this._entries.values()) {
            if (!entry.actor || !entry.rect)
                continue;

            const clone = new Clutter.Clone({
                source: entry.actor,
                reactive: false,
                visible: true,
            });

            clone.set_position(entry.rect.x, entry.rect.y);
            clone.set_size(entry.rect.width, entry.rect.height);

            this._layer.add_child(clone);
        }

        this._layer.show();
    }

    ensureDuringOverviewAnimation() {
        if (!this._enabled())
            return;

        if (!Main.overview.visible)
            return;

        if (!this._layer)
            this.rebuild();
    }

    destroyLayer() {
        if (!this._layer)
            return;

        this._layer.destroy();
        this._layer = null;
    }
}

class WorkspacePreviewWallpaperPatch {
    constructor(entries, featureSettings) {
        this._entries = entries;
        this._featureSettings = featureSettings;
        this._originalWorkspaceInit = null;
        this._workspaces = new Set();
        this._destroySignals = new Map();
        this._overviewSignals = [];
        this._animationSyncSourceId = 0;
        this._controlsStateAdjustment = null;

        this._debug = true;
    }

    _enabled() {
        return this._featureSettings.workspacePatch;
    }

    enable() {
        if (!this._enabled())
            return;

        if (this._originalWorkspaceInit)
            return;

        const patch = this;

        this._originalWorkspaceInit = Workspace.Workspace.prototype._init;
        this._bindControlsStateAdjustment();

        Workspace.Workspace.prototype._init = function (...args) {
            patch._originalWorkspaceInit.apply(this, args);

            const monitorIndex = patch._guessWorkspaceMonitorIndexFromArgs(this, args);

            this._nettleWorkspaceMonitorIndex = monitorIndex;

            GLib.idle_add(GLib.PRIORITY_DEFAULT_IDLE, () => {
                patch._patchWorkspace(this, monitorIndex);
                patch.rebuildAll();
                return GLib.SOURCE_REMOVE;
            });
        };

        this._overviewSignals.push([
            Main.overview,
            Main.overview.connect('showing', () => {
                this._bindControlsStateAdjustment?.();
        
                if (!this._shouldCreatePatch()) {
                    if (!this._shouldKeepPatch())
                        this._unpatchAllWorkspaces();
        
                    return;
                }
        
                this.scanExistingWorkspaces();
                this.rebuildAll();
        
                GLib.idle_add(GLib.PRIORITY_HIGH_IDLE, () => {
                    this._bindControlsStateAdjustment?.();
                
                    if (!this._shouldCreatePatch()) {
                        if (!this._shouldKeepPatch())
                            this._unpatchAllWorkspaces();
                
                        return GLib.SOURCE_REMOVE;
                    }
                
                    if (this._workspaces.size === 0) {
                        this.scanExistingWorkspaces();
                        this.rebuildAll();
                    } else {
                        this.syncAll();
                    }
                
                    return GLib.SOURCE_REMOVE;
                });
            }),
        ]);
        
        this._overviewSignals.push([
            Main.overview,
            Main.overview.connect('shown', () => {
                this._bindControlsStateAdjustment?.();
        
                if (!this._shouldCreatePatch()) {
                    if (!this._shouldKeepPatch())
                        this._unpatchAllWorkspaces();
        
                    return;
                }
        
                if (this._workspaces.size === 0) {
                    this.scanExistingWorkspaces();
                    this.rebuildAll();
                } else {
                    this.syncAll();
                }
            }),
        ]);

        this._overviewSignals.push([
            Main.overview,
            Main.overview.connect('hiding', () => {
                // Keep video actor alive during the zoom-in animation.
                // Only sync existing geometry; do not rebuild or destroy.
                for (const workspace of this._workspaces) {
                    if (!workspace?._nettlePreview)
                        continue;
        
                    this._syncOverlayGeometry(workspace);
                    this._syncPreviewCloneGeometry(workspace);
                }
            }),
        ]);

        this._overviewSignals.push([
            Main.overview,
            Main.overview.connect('hidden', () => {
                this._unpatchAllWorkspaces();
            }),
        ]);
    }

    disable() {        
        if (this._originalWorkspaceInit) {
            Workspace.Workspace.prototype._init = this._originalWorkspaceInit;
            this._originalWorkspaceInit = null;
        }

        for (const [obj, id] of this._overviewSignals) {
            try {
                obj.disconnect(id);
            } catch (_e) {
            }
        }

        this._overviewSignals = [];

        for (const workspace of [...this._workspaces])
            this._unpatchWorkspace(workspace);

        this._workspaces.clear();
        this._destroySignals.clear();
    }

    scanExistingWorkspaces() {
        if (!this._shouldCreatePatch())
            return;
    
        const overviewGroup = Main.layoutManager.overviewGroup;
    
        if (!overviewGroup)
            return;
    
        this._scanActorTree(overviewGroup);
    }
    
    rebuildAll() {
        if (!this._shouldCreatePatch())
            return;
    
        this.scanExistingWorkspaces();
    
        for (const workspace of this._workspaces)
            this._rebuildWorkspace(workspace);
    }

    _scanActorTree(actor) {
        if (!actor)
            return;

        if (this._isWorkspaceActor(actor)) {
            const monitorIndex = this._guessWorkspaceMonitorIndex(actor);
            this._patchWorkspace(actor, monitorIndex);
        }

        const children = actor.get_children?.() ?? [];

        for (const child of children)
            this._scanActorTree(child);
    }

    _getControlsStateAdjustment() {
        try {
            return Main.overview._overview?.controls?._stateAdjustment ?? null;
        } catch (_e) {
            return null;
        }
    }
    
    _bindControlsStateAdjustment() {
        const adjustment = this._getControlsStateAdjustment();
    
        if (!adjustment || adjustment === this._controlsStateAdjustment)
            return;
    
        this._controlsStateAdjustment = adjustment;
    
        const id = adjustment.connect('notify::value', () => {
            this._onControlsStateChanged();
        });
    
        this._overviewSignals.push([adjustment, id]);
    }
    
    _onControlsStateChanged() {
        if (this._isAppGridActiveOrTarget()) {
            this._unpatchAllWorkspaces();
            return;
        }
    
        if (this._shouldCreatePatch()) {
            if (this._workspaces.size === 0) {
                this.scanExistingWorkspaces();
                this.rebuildAll();
            } else {
                this.syncAll();
            }
    
            return;
        }
    
        if (this._shouldKeepPatch()) {
            this.syncAll();
            return;
        }
    
        this._unpatchAllWorkspaces();
    }

    _isAppGridActiveOrTarget() {
        try {
            const adjustment = this._getControlsStateAdjustment();
            const params = adjustment?.getStateTransitionParams?.();
    
            if (!params)
                return false;
    
            const isAppGrid =
                params.currentState > OverviewControls.ControlsState.WINDOW_PICKER ||
                params.finalState > OverviewControls.ControlsState.WINDOW_PICKER;
    
            if (isAppGrid)
                this._log(
                    `app grid active/target: current=${params.currentState}, ` +
                    `final=${params.finalState}, transitioning=${params.transitioning}`
                );
    
            return isAppGrid;
        } catch (e) {
            this._log(`failed to read overview controls state: ${e}`);
            return false;
        }
    }

    _isWorkspaceActor(actor) {
        try {
            if (actor instanceof Workspace.Workspace)
                return true;
        } catch (_e) {
        }

        const className = actor.constructor?.name ?? '';

        return className === 'Workspace';
    }

    _patchWorkspace(workspace, monitorIndex) {
        if (!workspace || workspace._nettlePreviewPatched)
            return;

        const background = this._findWorkspaceBackground(workspace);

        if (!background) {
            this._log('no workspace background found');
            return;
        }

        const host = background._bin;
        const belowActor = background._backgroundGroup;

        if (!host || !belowActor) {
            this._log('WorkspaceBackground internals unavailable');
            return;
        }

        const originalBackgroundGroupOpacity = belowActor.opacity;

        const overlay = new Clutter.Actor({
            reactive: false,
            visible: true,
            clip_to_allocation: true,
        });

        try {
            host.insert_child_above(overlay, belowActor);
        } catch (e) {
            this._log(`insert overlay inside WorkspaceBackground failed: ${e}`);
            return;
        }

        workspace._nettlePreviewPatched = true;
        workspace._nettlePreview = {
            host,
            background,
            overlay,
            monitorIndex,
            clones: [],
            allocationSignalIds: [],
            syncSourceId: 0,
            rebuildSourceId: 0,
            originalBackgroundGroupOpacity,
        };

        const syncOnly = () => {
            this._queueSyncWorkspace(workspace);
        };
        
        for (const target of [background, host, overlay]) {
            try {
                const id = target.connect('notify::allocation', syncOnly);
                workspace._nettlePreview.allocationSignalIds.push([target, id]);
            } catch (_e) {
            }
        }

        const destroyId = workspace.connect('destroy', () => {
            this._unpatchWorkspace(workspace);
        });

        this._destroySignals.set(workspace, destroyId);
        this._workspaces.add(workspace);

        this._syncOverlayGeometry(workspace);
        this._rebuildWorkspace(workspace);

        this._log(`patched workspace preview, monitorIndex=${monitorIndex}`);
    }

    _unpatchWorkspace(workspace) {
        if (!workspace?._nettlePreview)
            return;

        const preview = workspace._nettlePreview;

        if (preview.syncSourceId) {
            try {
                GLib.source_remove(preview.syncSourceId);
            } catch (_e) {
            }
        
            preview.syncSourceId = 0;
        }

        if (preview.rebuildSourceId) {
            try {
                GLib.source_remove(preview.rebuildSourceId);
            } catch (_e) {
            }
        
            preview.rebuildSourceId = 0;
        }

        for (const record of preview.clones) {
            try {
                if (record?.mask) {
                    try {
                        record.mask.remove_effect_by_name?.('nettle-rounded-clip');
                    } catch (_e) {
                    }
        
                    record.mask.destroy();
                } else if (record?.clone) {
                    record.clone.destroy();
                } else if (record?.destroy) {
                    record.destroy();
                }
            } catch (_e) {
            }
        
            if (record) {
                record.mask = null;
                record.clone = null;
                record.effect = null;
                record.entry = null;
                record.monitor = null;
            }
        }
        
        preview.clones = [];

        for (const [target, id] of preview.allocationSignalIds) {
            try {
                target.disconnect(id);
            } catch (_e) {
            }
        }

        preview.allocationSignalIds = [];

        try {
            preview.background._backgroundGroup.opacity =
                preview.originalBackgroundGroupOpacity ?? 255;
        } catch (_e) {
        }

        try {
            preview.overlay.destroy();
        } catch (_e) {
        }

        const destroyId = this._destroySignals.get(workspace);

        if (destroyId) {
            try {
                workspace.disconnect(destroyId);
            } catch (_e) {
            }
        }

        this._destroySignals.delete(workspace);
        this._workspaces.delete(workspace);

        workspace._nettlePreviewPatched = false;
        workspace._nettlePreview = null;
    }

    _unpatchAllWorkspaces() {
        for (const workspace of [...this._workspaces])
            this._unpatchWorkspace(workspace);
    
        this._workspaces.clear();
        this._destroySignals.clear();
    }

    _shouldCreatePatch() {
        if (!this._enabled())
            return false;
    
        if (this._entries.size === 0)
            return false;
    
        // Only create/rebuild while overview is opening/open.
        if (!Main.overview.visibleTarget)
            return false;
    
        // Do not create/rebuild for app grid.
        if (this._isAppGridActiveOrTarget())
            return false;
    
        return true;
    }
    
    _shouldKeepPatch() {
        if (!this._enabled())
            return false;
    
        if (this._entries.size === 0)
            return false;
    
        // If overview is still visible OR transitioning, keep existing actors alive.
        // This is the important part for the close animation.
        if (!Main.overview.visible && !Main.overview.visibleTarget)
            return false;
    
        // App grid is special: remove the patch there.
        if (this._isAppGridActiveOrTarget())
            return false;
    
        return true;
    }

    _queueRebuildWorkspace(workspace) {
        const preview = workspace?._nettlePreview;
    
        if (!preview)
            return;
    
        if (preview.rebuildSourceId)
            return;
    
        preview.rebuildSourceId = GLib.idle_add(GLib.PRIORITY_DEFAULT_IDLE, () => {
            preview.rebuildSourceId = 0;
    
            if (workspace._nettlePreview) {
                this._syncOverlayGeometry(workspace);
                this._rebuildWorkspace(workspace);
            }
    
            return GLib.SOURCE_REMOVE;
        });
    }

    _queueSyncWorkspace(workspace) {
        const preview = workspace?._nettlePreview;
    
        if (!preview)
            return;
    
        if (preview.syncSourceId)
            return;
    
        preview.syncSourceId = GLib.idle_add(GLib.PRIORITY_HIGH_IDLE, () => {
            preview.syncSourceId = 0;
    
            if (workspace._nettlePreview) {
                this._syncOverlayGeometry(workspace);
                this._syncPreviewCloneGeometry(workspace);
            }
    
            return GLib.SOURCE_REMOVE;
        });
    }

    _findWorkspaceBackground(workspace) {
        if (workspace._background)
            return workspace._background;

        for (const child of workspace.get_children?.() ?? []) {
            const name = `${child.name ?? ''}`.toLowerCase();
            const className = `${child.constructor?.name ?? ''}`.toLowerCase();

            if (name.includes('background') || className.includes('background'))
                return child;
        }

        return this._findBackgroundRecursive(workspace);
    }

    _findBackgroundRecursive(actor) {
        const children = actor.get_children?.() ?? [];

        for (const child of children) {
            const name = `${child.name ?? ''}`.toLowerCase();
            const className = `${child.constructor?.name ?? ''}`.toLowerCase();

            if (name.includes('background') || className.includes('background'))
                return child;

            const nested = this._findBackgroundRecursive(child);

            if (nested)
                return nested;
        }

        return null;
    }

    _syncOverlayGeometry(workspace) {
        const preview = workspace._nettlePreview;
    
        if (!preview)
            return;
    
        const {background, overlay, host} = preview;
    
        let width = host.width;
        let height = host.height;
    
        try {
            const box = host.get_allocation_box();
            width = box.x2 - box.x1;
            height = box.y2 - box.y1;
        } catch (_e) {
        }
    
        if (width <= 0 || height <= 0)
            return;
    
        overlay.set_position(0, 0);
        overlay.set_size(width, height);
    
        try {
            host.set_child_above_sibling(overlay, background._backgroundGroup);
        } catch (_e) {
        }
    }

    _actorStageRect(actor) {
        if (!actor)
            return null;

        try {
            const [x, y] = actor.get_transformed_position();
            const [width, height] = actor.get_transformed_size();

            if (width <= 0 || height <= 0)
                return null;

            return {x, y, width, height};
        } catch (_e) {
            return null;
        }
    }

    _monitorIndexForStageRect(rect) {
        if (!rect)
            return -1;

        const monitors = Main.layoutManager.monitors;

        const cx = rect.x + rect.width / 2;
        const cy = rect.y + rect.height / 2;

        for (let i = 0; i < monitors.length; i++) {
            const monitor = monitors[i];

            if (
                cx >= monitor.x &&
                cx < monitor.x + monitor.width &&
                cy >= monitor.y &&
                cy < monitor.y + monitor.height
            ) {
                return i;
            }
        }

        // Fallback to best overlap.
        let bestIndex = -1;
        let bestArea = 0;

        for (let i = 0; i < monitors.length; i++) {
            const monitor = monitors[i];

            const left = Math.max(rect.x, monitor.x);
            const top = Math.max(rect.y, monitor.y);
            const right = Math.min(rect.x + rect.width, monitor.x + monitor.width);
            const bottom = Math.min(rect.y + rect.height, monitor.y + monitor.height);

            const area =
                Math.max(0, right - left) *
                Math.max(0, bottom - top);

            if (area > bestArea) {
                bestArea = area;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    _guessWorkspaceMonitorIndex(workspace) {
        const preview = workspace?._nettlePreview;

        // Best method: use actual stage geometry of the rounded preview/background.
        const stageRects = [
            this._actorStageRect(preview?.background),
            this._actorStageRect(preview?.host),
            this._actorStageRect(workspace),
        ];

        for (const rect of stageRects) {
            const index = this._monitorIndexForStageRect(rect);

            if (index >= 0)
                return index;
        }

        // Fallbacks only.
        const candidates = [
            workspace?._monitorIndex,
            workspace?.monitorIndex,
            workspace?._monitor?.index,
            workspace?.monitor?.index,
            workspace?._nettleWorkspaceMonitorIndex,
        ];

        for (const value of candidates) {
            if (
                typeof value === 'number' &&
                value >= 0 &&
                value < Main.layoutManager.monitors.length
            ) {
                return value;
            }
        }

        return Main.layoutManager.primaryIndex ?? 0;
    }

    _sameMonitorRect(a, b) {
        if (!a || !b)
            return false;

        return (
            a.x === b.x &&
            a.y === b.y &&
            a.width === b.width &&
            a.height === b.height
        );
    }

    _rebuildWorkspace(workspace) {
        const preview = workspace?._nettlePreview;

        if (!preview)
            return;

        const {overlay} = preview;

        this._syncOverlayGeometry(workspace);

        for (const record of preview.clones) {
            try {
                if (record?.mask) {
                    try {
                        record.mask.remove_effect_by_name?.('nettle-rounded-clip');
                    } catch (_e) {
                    }
        
                    record.mask.destroy();
                } else if (record?.clone) {
                    record.clone.destroy();
                } else if (record?.destroy) {
                    record.destroy();
                }
            } catch (_e) {
            }
        
            if (record) {
                record.mask = null;
                record.clone = null;
                record.effect = null;
                record.entry = null;
                record.monitor = null;
            }
        }
        
        preview.clones = [];

        const monitorIndex = this._guessWorkspaceMonitorIndex(workspace);
        preview.monitorIndex = monitorIndex;

        const monitor = Main.layoutManager.monitors[monitorIndex];

        if (!monitor) {
            this._log(`no monitor for workspace monitorIndex=${monitorIndex}`);
            return;
        }

        const overlayWidth = overlay.width;
        const overlayHeight = overlay.height;

        if (overlayWidth <= 0 || overlayHeight <= 0) {
            this._log('overlay has zero size');
            return;
        }

        const workspaceMonitorRect = {
            x: monitor.x,
            y: monitor.y,
            width: monitor.width,
            height: monitor.height,
        };

        let clonesAdded = 0;

        for (const entry of this._entries.values()) {
            if (!entry.actor || !entry.rect)
                continue;

            // Important:
            // Compare monitor geometry, not only monitor index.
            // GNOME's workspace preview monitor indexing can differ from
            // the monitor index we computed from the wallpaper rectangle.
            const sameMonitor =
                entry.monitorIndex === monitorIndex ||
                this._sameMonitorRect(entry.monitorRect, workspaceMonitorRect);

            if (!sameMonitor)
                continue;

            if (!this._rectIntersectsMonitor(entry.rect, monitor))
                continue;

            this._addScaledClone(preview, entry, monitor);
            clonesAdded++;
        }

        if (clonesAdded > 0) {
            try {
                preview.background._backgroundGroup.opacity = 0;
            } catch (_e) {
            }
        
            overlay.show();
        } else {
            try {
                preview.background._backgroundGroup.opacity =
                    preview.originalBackgroundGroupOpacity ?? 255;
            } catch (_e) {
            }
        
            overlay.hide();
        }
        
        this._log(
            `rebuilt workspace preview, clones=${clonesAdded}, ` +
            `workspaceMonitor=${monitorIndex}, ` +
            `monitorRect=${monitor.x},${monitor.y},${monitor.width}x${monitor.height}`
        );
    }

    _guessWorkspaceMonitorIndexFromArgs(workspace, args) {
        for (const arg of args) {
            if (
                typeof arg === 'number' &&
                arg >= 0 &&
                arg < Main.layoutManager.monitors.length
            ) {
                return arg;
            }
        }

        return this._guessWorkspaceMonitorIndex(workspace);
    }

    _addScaledClone(preview, entry, monitor) {
        const mask = new Clutter.Actor({
            reactive: false,
            visible: true,
            clip_to_allocation: true,
        });
    
        let roundedEffect = null;
    
        try {
            roundedEffect = this._createRoundedClipEffect(
                1,
                1,
                WorkspacePreviewRadius
            );
    
            mask.add_effect_with_name('nettle-rounded-clip', roundedEffect);
        } catch (e) {
            this._log(`rounded shader failed, showing square preview: ${e}`);
        }
    
        const clone = new Clutter.Clone({
            source: entry.actor,
            reactive: false,
            visible: true,
        });
    
        mask.add_child(clone);
        preview.overlay.add_child(mask);
    
        const record = {
            mask,
            clone,
            effect: roundedEffect,
            entry,
            monitor,
        };
    
        preview.clones.push(record);
    
        this._syncCloneRecordGeometry(preview, record);
    }
    
    _syncPreviewCloneGeometry(workspace) {
        const preview = workspace?._nettlePreview;
    
        if (!preview)
            return;
    
        for (const record of preview.clones) {
            if (record?.clone)
                this._syncCloneRecordGeometry(preview, record);
        }
    }
    
    _syncCloneRecordGeometry(preview, record) {
        const {overlay} = preview;
        const {entry, monitor, mask, clone, effect} = record;
    
        if (!overlay || !entry?.rect || !monitor || !mask || !clone)
            return;
    
        const overlayWidth = overlay.width;
        const overlayHeight = overlay.height;
    
        if (overlayWidth <= 0 || overlayHeight <= 0)
            return;
    
        const scaleX = overlayWidth / monitor.width;
        const scaleY = overlayHeight / monitor.height;
    
        const x = Math.floor((entry.rect.x - monitor.x) * scaleX);
        const y = Math.floor((entry.rect.y - monitor.y) * scaleY);
        const width = Math.ceil(entry.rect.width * scaleX);
        const height = Math.ceil(entry.rect.height * scaleY);
    
        if (width <= 0 || height <= 0)
            return;
    
        const sourceWidth = entry.actor.width || entry.rect.width || monitor.width;
        const sourceHeight = entry.actor.height || entry.rect.height || monitor.height;
    
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return;
    
        mask.set_position(x, y);
        mask.set_size(width, height);
    
        clone.set_position(0, 0);
        clone.set_size(sourceWidth, sourceHeight);
        clone.set_scale(width / sourceWidth, height / sourceHeight);
    
        if (effect) {
            this._setShaderFloat(effect, 'width', width);
            this._setShaderFloat(effect, 'height', height);
            this._setShaderFloat(effect, 'radius', WorkspacePreviewRadius);
            this._setShaderFloat(effect, 'softness', WorkspacePreviewEdgeSoftness);
        }
    }

    _setShaderFloat(effect, name, value) {
        const gvalue = new GObject.Value();
        gvalue.init(GObject.TYPE_FLOAT);
        gvalue.set_float(Number(value));
        effect.set_uniform_value(name, gvalue);
    }
    
    _setShaderInt(effect, name, value) {
        const gvalue = new GObject.Value();
        gvalue.init(GObject.TYPE_INT);
        gvalue.set_int(Number(value));
        effect.set_uniform_value(name, gvalue);
    }
    
    _createRoundedClipEffect(width, height, radius) {
        const effect = new Clutter.ShaderEffect({
            shader_type: Cogl.ShaderType.FRAGMENT,
        });
    
        effect.set_shader_source(`
            uniform sampler2D tex;
            uniform float width;
            uniform float height;
            uniform float radius;
            uniform float softness;
    
            float roundedRectSdf(vec2 p, vec2 size, float r) {
                vec2 halfSize = size * 0.5;
                vec2 q = abs(p - halfSize) - (halfSize - vec2(r));
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }
    
            void main() {
                vec2 uv = cogl_tex_coord_in[0].st;
                vec4 color = texture2D(tex, uv) * cogl_color_in;
    
                vec2 p = vec2(uv.x * width, uv.y * height);
                float dist = roundedRectSdf(p, vec2(width, height), radius);
    
                float alpha = 1.0 - smoothstep(-softness, softness, dist);
    
                if (alpha <= 0.001)
                    discard;
    
                cogl_color_out = vec4(color.rgb * alpha, color.a * alpha);
            }
        `);
    
        this._setShaderInt(effect, 'tex', 0);
        this._setShaderFloat(effect, 'width', width);
        this._setShaderFloat(effect, 'height', height);
        this._setShaderFloat(effect, 'radius', radius);
        this._setShaderFloat(effect, 'softness', WorkspacePreviewEdgeSoftness);
    
        return effect;
    }

    _rectIntersectsMonitor(rect, monitor) {
        const rectRight = rect.x + rect.width;
        const rectBottom = rect.y + rect.height;
        const monitorRight = monitor.x + monitor.width;
        const monitorBottom = monitor.y + monitor.height;

        return !(
            rectRight <= monitor.x ||
            rect.x >= monitorRight ||
            rectBottom <= monitor.y ||
            rect.y >= monitorBottom
        );
    }

    _log(message) {
        if (this._debug)
            console.log(`[NettleExtension:WorkspacePreview] ${message}`);
    }
}

class NettleDbusService {
    constructor(extension) {
        this._extension = extension;
    }

    Attach(token, x, y, width, height) {
        return this._extension.attach(token, x, y, width, height);
    }

    Detach(token) {
        return this._extension.detach(token);
    }

    DetachAll() {
        this._extension.detachAll();
        return [true, 'detached all'];
    }
}

export default class NettleExtension extends Extension {
    enable() {
        this._entries = new Map();
        this._pending = new Map();
        this._trackedWindows = new Map();

        this._settings = this.getSettings(SETTINGS_SCHEMA);
        this._settingsSignalIds = [];

        this._featureSettings = {
            overviewBackground: false,
            workspacePatch: true,
        };

        this._loadFeatureSettings();

        this._filter = new WindowFilter();
        this._filter.enable();

        this._overviewCloneLayer = null;
        this._workspacePreviewPatch = null;

        this._settingsSignalIds.push(
            this._settings.connect(`changed::${SETTING_ADD_OVERVIEW}`, () => {
                this._loadFeatureSettings();
                this._syncFeatureSettings();
            })
        );

        this._settingsSignalIds.push(
            this._settings.connect(`changed::${SETTING_WORKSPACE_PATCH}`, () => {
                this._loadFeatureSettings();
                this._syncFeatureSettings();
            })
        );

        this._syncFeatureSettings();

        this._windowCreatedId = global.display.connect('window-created', () => {
            GLib.idle_add(GLib.PRIORITY_DEFAULT, () => {
                this._attachPending();
                this._lowerAll();
                this._overviewCloneLayer?.ensureDuringOverviewAnimation();
                return GLib.SOURCE_REMOVE;
            });
        });

        this._trackExistingWindows();

        this._windowTrackingCreatedId = global.display.connect('window-created', (_display, metaWindow) => {
            GLib.idle_add(GLib.PRIORITY_DEFAULT_IDLE, () => {
                this._trackWindow(metaWindow);
                return GLib.SOURCE_REMOVE;
            });
        });                             

        this._restackedId = global.display.connect('restacked', () => {
            this._lowerAll();
        });

        this._busOwnerId = Gio.bus_own_name(
            Gio.BusType.SESSION,
            BUS_NAME,
            Gio.BusNameOwnerFlags.REPLACE,
            this._onBusAcquired.bind(this),
            null,
            null
        );

        console.log('[NettleExtension] enabled');
    }

    disable() {
        this.detachAll();

        if (this._settings && this._settingsSignalIds) {
            for (const id of this._settingsSignalIds) {
                try {
                    this._settings.disconnect(id);
                } catch (_e) {
                }
            }
        }
        
        this._settingsSignalIds = [];
        this._settings = null;
        this._featureSettings = null;

        if (this._windowCreatedId) {
            global.display.disconnect(this._windowCreatedId);
            this._windowCreatedId = 0;
        }

        if (this._restackedId) {
            global.display.disconnect(this._restackedId);
            this._restackedId = 0;
        }

        if (this._overviewCloneLayer) {
            this._overviewCloneLayer.disable();
            this._overviewCloneLayer = null;
        }

        if (this._workspacePreviewPatch) {
            this._workspacePreviewPatch.disable();
            this._workspacePreviewPatch = null;
        }

        if (this._dbusObject) {
            this._dbusObject.unexport();
            this._dbusObject = null;
        }

        if (this._busOwnerId) {
            Gio.bus_unown_name(this._busOwnerId);
            this._busOwnerId = 0;
        }

        if (this._filter) {
            this._filter.disable();
            this._filter = null;
        }

        if (this._windowTrackingCreatedId) {
            global.display.disconnect(this._windowTrackingCreatedId);
            this._windowTrackingCreatedId = 0;
        }
        
        this._untrackAllWindows();

        this._entries = null;
        this._pending = null;

        console.log('[NettleExtension] disabled');
    }

    _onBusAcquired(connection) {
        const service = new NettleDbusService(this);

        this._dbusObject = Gio.DBusExportedObject.wrapJSObject(
            DBUS_XML,
            service
        );

        this._dbusObject.export(connection, OBJECT_PATH);

        console.log('[NettleExtension] D-Bus exported');
    }

    _loadFeatureSettings() {
        try {
            this._featureSettings.overviewBackground =
                this._settings.get_boolean(SETTING_ADD_OVERVIEW);
        } catch (_e) {
            this._featureSettings.overviewBackground = false;
        }
    
        try {
            this._featureSettings.workspacePatch =
                this._settings.get_boolean(SETTING_WORKSPACE_PATCH);
        } catch (_e) {
            this._featureSettings.workspacePatch = true;
        }
    }

    _syncFeatureSettings() {
        if (this._featureSettings.overviewBackground) {
            if (!this._overviewCloneLayer) {
                this._overviewCloneLayer = new OverviewCloneLayer(
                    this._entries,
                    this._featureSettings
                );
    
                this._overviewCloneLayer.enable();
            }
    
            if (Main.overview.visible)
                this._overviewCloneLayer?.rebuild();
        } else {
            if (this._overviewCloneLayer) {
                this._overviewCloneLayer.disable();
                this._overviewCloneLayer = null;
            }
        }
    
        if (this._featureSettings.workspacePatch) {
            if (!this._workspacePreviewPatch) {
                this._workspacePreviewPatch = new WorkspacePreviewWallpaperPatch(
                    this._entries,
                    this._featureSettings
                );
    
                this._workspacePreviewPatch.enable();
            }
    
            if (Main.overview.visible || Main.overview.visibleTarget)
                this._workspacePreviewPatch.rebuildAll();
        } else {
            if (this._workspacePreviewPatch) {
                this._workspacePreviewPatch.disable();
                this._workspacePreviewPatch = null;
            }
        }
    }

    attach(token, x, y, width, height) {
        if (!token)
            return [false, 'token is empty'];

        if (width <= 0 || height <= 0)
            return [false, 'width and height must be positive'];

        const rect = {x, y, width, height};

        this._pending.set(token, rect);

        const found = this._findWindow(token);

        if (!found)
            return [true, `pending: no MPV window found for token "${token}" yet`];
        
        return this._attachWindow(token, found.actor, found.metaWindow, rect);
    }

    detach(token) {
        this._pending.delete(token);

        const entry = this._entries.get(token);

        if (!entry)
            return [true, `nothing attached for token "${token}"`];

        this._detachEntry(token, entry, true);

        if (this._overviewCloneLayer)
            this._overviewCloneLayer.rebuild();

        if (this._workspacePreviewPatch)
            this._workspacePreviewPatch.rebuildAll();

        return [true, `detached "${token}"`];
    }

    detachAll() {
        for (const [token, entry] of this._entries)
            this._detachEntry(token, entry, true);
    
        this._entries.clear();
        this._pending.clear();
    
        if (this._overviewCloneLayer)
            this._overviewCloneLayer.destroyLayer();
    
        if (this._workspacePreviewPatch)
            this._workspacePreviewPatch.rebuildAll();
    }

    _attachPending() {
        for (const [token, rect] of this._pending) {
            if (this._entries.has(token))
                continue;

            const found = this._findWindow(token);

            if (found)
                this._attachWindow(token, found.actor, found.metaWindow, rect);
        }
    }

    _attachWindow(token, actor, metaWindow, rect) {
        if (this._entries.has(token))
            this._detachEntry(token, this._entries.get(token), false);

        try {
            this._applyWallpaperRules(actor, metaWindow, rect);
        } catch (e) {
            return [false, `failed to apply wallpaper rules: ${e}`];
        }

        const destroyId = actor.connect('destroy', () => {
            const entry = this._entries.get(token);

            if (!entry)
                return;

            this._filter.remove(entry.metaWindow);
            this._entries.delete(token);
            this._pending.delete(token);

            if (this._overviewCloneLayer)
                this._overviewCloneLayer.rebuild();

            if (this._workspacePreviewPatch)
                this._workspacePreviewPatch.rebuildAll();

            console.log(`[NettleExtension] MPV actor destroyed: ${token}`);
        });

        const raisedId = metaWindow.connect('raised', () => {
            this._lowerEntry(token);
        });

        const lowerRetryId = this._addShortLowerRetry(token);

        this._filter.add(metaWindow);
        this._filter.purgeFromExistingWorkspaces(metaWindow);
        this._untrackWindow(metaWindow);

        const monitorIndex = this._monitorIndexForRect(rect);
        const monitor = Main.layoutManager.monitors[monitorIndex];

        const monitorRect = monitor
            ? {
                x: monitor.x,
                y: monitor.y,
                width: monitor.width,
                height: monitor.height,
            }
            : null;

        this._entries.set(token, {
            actor,
            metaWindow,
            rect,
            monitorIndex,
            monitorRect,
            destroyId,
            raisedId,
            lowerRetryId,
        });

        console.log(
            `[NettleExtension] attached ${token} to monitor ${monitorIndex} ` +
            `rect=${rect.x},${rect.y},${rect.width}x${rect.height}`
        );

        console.log(`[NettleExtension] attached ${token} to monitor ${monitorIndex}`);

        this._pending.delete(token);

        if (this._overviewCloneLayer)
            this._overviewCloneLayer.rebuild();

        if (this._workspacePreviewPatch)
            this._workspacePreviewPatch.rebuildAll();

        return [true, `attached "${token}"`];
    }

    _detachEntry(token, entry, restoreWindow) {
        const {actor, metaWindow, destroyId, raisedId, lowerRetryId} = entry;

        if (lowerRetryId)
            GLib.source_remove(lowerRetryId);

        try {
            if (destroyId)
                actor.disconnect(destroyId);
        } catch (_e) {
        }

        try {
            if (raisedId)
                metaWindow.disconnect(raisedId);
        } catch (_e) {
        }

        this._filter.remove(metaWindow);

        if (restoreWindow) {
            try {
                metaWindow.set_accept_focus?.(true);
                metaWindow.focus_on_click = true;
                metaWindow.unstick?.();
            } catch (_e) {
            }
        }

        this._entries.delete(token);
    }

    _applyWallpaperRules(actor, metaWindow, rect) {
        try {
            metaWindow.move_resize_frame(
                false,
                rect.x,
                rect.y,
                rect.width,
                rect.height
            );
        } catch (_e) {
            actor.set_position(rect.x, rect.y);
            actor.set_size(rect.width, rect.height);
        }
    
        metaWindow.stick();
        metaWindow.lower();
    
        metaWindow.focus_on_click = false;
    
        try {
            metaWindow.set_accept_focus(false);
        } catch (_e) {
        }
    
        try {
            metaWindow.set_input_region(null);
        } catch (_e) {
        }
    
        actor.reactive = false;
        actor.show();
    }

    _addShortLowerRetry(token) {
        let count = 0;

        const sourceId = GLib.timeout_add(GLib.PRIORITY_DEFAULT, 100, () => {
            const entry = this._entries.get(token);

            if (!entry)
                return GLib.SOURCE_REMOVE;

            try {
                entry.metaWindow.lower();
            } catch (_e) {
                return GLib.SOURCE_REMOVE;
            }

            count++;

            // Only retry briefly after attach.
            // After that, "raised", "restacked", and "window-created" keep it handled.
            return count < 5 ? GLib.SOURCE_CONTINUE : GLib.SOURCE_REMOVE;
        });

        return sourceId;
    }

    _lowerEntry(token) {
        const entry = this._entries.get(token);

        if (!entry)
            return;

        try {
            entry.metaWindow.lower();
        } catch (_e) {
        }
    }

    _lowerAll() {
        for (const entry of this._entries.values()) {
            try {
                entry.metaWindow.lower();
            } catch (_e) {
            }
        }
    }

    _monitorIndexForRect(rect) {
        const monitors = Main.layoutManager.monitors;
    
        let bestIndex = -1;
        let bestArea = 0;
    
        for (let i = 0; i < monitors.length; i++) {
            const monitor = monitors[i];
    
            const left = Math.max(rect.x, monitor.x);
            const top = Math.max(rect.y, monitor.y);
            const right = Math.min(rect.x + rect.width, monitor.x + monitor.width);
            const bottom = Math.min(rect.y + rect.height, monitor.y + monitor.height);
    
            const width = Math.max(0, right - left);
            const height = Math.max(0, bottom - top);
            const area = width * height;
    
            if (area > bestArea) {
                bestArea = area;
                bestIndex = i;
            }
        }
    
        return bestIndex;
    }

    _findWindow(token) {
        for (const actor of global.get_window_actors()) {
            const metaWindow = actor.get_meta_window?.() ?? actor.meta_window;

            if (!metaWindow)
                continue;

            const title = metaWindow.get_title?.() ?? '';
            const wmClass = metaWindow.get_wm_class?.() ?? '';

            if (title.includes(token) || wmClass.includes(token))
                return {actor, metaWindow};
        }

        return null;
    }

    _purgeWallpaperWindowsFromWorkspaces() {
        if (!this._filter)
            return;
    
        for (const entry of this._entries.values()) {
            if (entry.metaWindow)
                this._filter.purgeFromExistingWorkspaces(entry.metaWindow);
        }
    }

    _trackExistingWindows() {
        for (const actor of global.get_window_actors()) {
            const metaWindow = actor.get_meta_window?.() ?? actor.meta_window;
    
            if (metaWindow)
                this._trackWindow(metaWindow);
        }
    }
    
    _trackWindow(metaWindow) {
        if (!metaWindow)
            return;
    
        if (this._trackedWindows.has(metaWindow))
            return;
    
        if (this._isWallpaperMetaWindow(metaWindow))
            return;
    
        const signalIds = [];
    
        const emitLater = () => {
            GLib.idle_add(GLib.PRIORITY_DEFAULT_IDLE, () => {
                this._emitWindowStateChanged(metaWindow);
                return GLib.SOURCE_REMOVE;
            });
        };
    
        const connectSafe = signalName => {
            try {
                const id = metaWindow.connect(signalName, emitLater);
                signalIds.push(id);
            } catch (e) {
            }
        };
    
        // connectSafe('size-changed');
        connectSafe('notify::fullscreen');
        connectSafe('notify::maximized-horizontally');
        connectSafe('notify::maximized-vertically');
        connectSafe('notify::appears-focused');
        connectSafe('notify::minimized');
    
        try {
            const unmanagedId = metaWindow.connect('unmanaged', () => {
                this._untrackWindow(metaWindow);
            });
    
            signalIds.push(unmanagedId);
        } catch (_e) {
        }
    
        const initialState = this._getWindowState(metaWindow);
    
        this._trackedWindows.set(metaWindow, {
            signalIds,
            lastKey: this._stateKey(initialState),
        });
    
        // Emit initial state once, useful if app starts while something is already fullscreen.
        this._emitWindowStateChanged(metaWindow);
    }
    
    _untrackWindow(metaWindow) {
        const tracked = this._trackedWindows?.get(metaWindow);
    
        if (!tracked)
            return;
    
        for (const id of tracked.signalIds) {
            try {
                metaWindow.disconnect(id);
            } catch (_e) {
            }
        }
    
        this._trackedWindows.delete(metaWindow);
    }
    
    _untrackAllWindows() {
        if (!this._trackedWindows)
            return;
    
        for (const metaWindow of [...this._trackedWindows.keys()])
            this._untrackWindow(metaWindow);
    
        this._trackedWindows.clear();
    }
    
    _isWallpaperMetaWindow(metaWindow) {
        for (const entry of this._entries?.values?.() ?? []) {
            if (entry.metaWindow === metaWindow)
                return true;
        }
    
        const title = metaWindow.get_title?.() ?? '';
    
        return title.includes('nettle-');
    }
    
    _getWindowState(metaWindow) {
        const title = metaWindow.get_title?.() ?? '';
        const wmClass = metaWindow.get_wm_class?.() ?? '';
    
        let fullscreen = false;
        let maximized = false;
        let active = false;
        let monitorIndex = -1;
    
        try {
            if (typeof metaWindow.is_fullscreen === 'function')
                fullscreen = metaWindow.is_fullscreen();
            else
                fullscreen = !!metaWindow.fullscreen;
        } catch (_e) {
        }
    
        try {
            maximized =
                !!metaWindow.maximized_horizontally &&
                !!metaWindow.maximized_vertically;
        } catch (_e) {
        }
    
        try {
            active = global.display.focus_window === metaWindow;
        } catch (_e) {
        }
    
        try {
            monitorIndex = metaWindow.get_monitor();
        } catch (_e) {
        }
    
        return {
            title,
            wmClass,
            maximized,
            fullscreen,
            active,
            monitorIndex,
        };
    }
    
    _stateKey(state) {
        return [
            state.title,
            state.wmClass,
            state.maximized ? '1' : '0',
            state.fullscreen ? '1' : '0',
            state.active ? '1' : '0',
            `${state.monitorIndex}`,
        ].join('|');
    }
    
    _emitWindowStateChanged(metaWindow) {
        if (!this._dbusObject)
            return;
    
        if (!metaWindow)
            return;
    
        if (this._isWallpaperMetaWindow(metaWindow))
            return;
    
        const state = this._getWindowState(metaWindow);
        const key = this._stateKey(state);
    
        const tracked = this._trackedWindows?.get(metaWindow);
    
        // Avoid spamming identical states.
        if (tracked && tracked.lastKey === key)
            return;
    
        if (tracked)
            tracked.lastKey = key;
    
        try {
            this._dbusObject.emit_signal(
                'WindowStateChanged',
                new GLib.Variant('(ssbbbi)', [
                    state.title,
                    state.wmClass,
                    state.maximized,
                    state.fullscreen,
                    state.active,
                    state.monitorIndex,
                ])
            );
        } catch (e) {
            console.error(`[NettleExtension] failed to emit WindowStateChanged: ${e}`);
        }
    }
}