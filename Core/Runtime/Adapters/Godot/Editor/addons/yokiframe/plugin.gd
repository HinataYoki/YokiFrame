@tool
extends EditorPlugin

const MENU_ITEM = "YokiFrame/Open Panel"
const PACKAGE_ROOT_SETTING = "yokiframe/package_root"
const YOKI_YOKIFRAME_DIR = "YOKI_YOKIFRAME_DIR"
const YOKI_DIST_PATH = "YOKI_DIST_PATH"
const YOKI_OWNER_HWND = "YOKI_OWNER_HWND"
const PID_FILE = "tauri-editor.pid"
const PANEL_REQUEST_DIR = "panel"
const PANEL_SHOW_REQUEST_FILE = "show-window.json"
const GODOT_EDITOR_ENGINE_ID = "godot-editor"
const ADAPTER_VERSION = "2.0.0"
const HEARTBEAT_INTERVAL_SECONDS = 2.0
const COMMAND_POLL_INTERVAL_SECONDS = 0.1
const AUTOLOAD_RETRY_INTERVAL_SECONDS = 1.0
const AUTOLOAD_MAX_RETRY_COUNT = 30
const ADDONS_PLUGIN_DIR = "res://addons/yokiframe"
const ADDONS_PLUGIN_CONFIG_PATH = "res://addons/yokiframe/plugin.cfg"
const ADDONS_PLUGIN_SCRIPT_PATH = "res://addons/yokiframe/plugin.gd"
const EDITOR_PLUGINS_ENABLED_SETTING = "editor_plugins/enabled"
const DOTNET_ASSEMBLY_NAME_SETTING = "dotnet/project/assembly_name"
const DEFAULT_PACKAGE_ROOT = "res://Scripts/YokiFrame"
const INSTALLER_PACKAGE_ROOT = "res://addons/yokiframe/package/YokiFrame"
const REAL_PLUGIN_RELATIVE_PATH = "Core/Runtime/Adapters/Godot/Editor/addons/yokiframe/plugin.gd"
const AUTOLOAD_NAME = "YokiFrameGodotBootstrap"
const AUTOLOAD_SETTING = "autoload/YokiFrameGodotBootstrap"
const BOOTSTRAP_RELATIVE_PATH = "Core/Runtime/Adapters/Godot/Runtime/Core/Adapters/Runtime/GodotBootstrap.cs"
const LUBAN_SUPPORT_DEFINE = "YOKIFRAME_LUBAN_SUPPORT"
const LUBAN_PACKAGE_NAME = "com.code-philosophy.luban"
const LUBAN_RUNTIME_ASSEMBLY_NAME = "Luban.Runtime"
const LUBAN_RUNTIME_TYPE_NAME = "Luban.ByteBuf"
const DIRECTORY_BUILD_PROPS_FILE = "Directory.Build.props"
const GODOT_IMPLEMENTED_KITS_JSON = "[\"System\",\"Architecture\",\"EventKit\",\"FsmKit\",\"LogKit\",\"PoolKit\",\"ResKit\",\"SingletonKit\",\"ActionKit\",\"InputKit\",\"LocalizationKit\",\"SaveKit\",\"SceneKit\",\"SpatialKit\",\"TableKit\"]"
const GODOT_EDITOR_KIT_FEATURES_JSON = "{\"System\":[\"commands\",\"bridge_status\"],\"EventKit\":[\"static_scan\"],\"TableKit\":[\"tauri_config\",\"registry_optional_dependencies\"]}"

var _heartbeat_timer: Timer = null
var _command_poll_timer: Timer = null
var _autoload_timer: Timer = null
var _autoload_retry_count: int = 0
var _started_at_utc: String = ""

func _enter_tree() -> void:
	_started_at_utc = _utc_now_string()
	_ensure_addons_entrypoint()
	_schedule_godot_bootstrap_autoload()
	add_tool_menu_item(MENU_ITEM, _open_panel)
	set_process_shortcut_input(true)
	_start_heartbeat_timer()
	_start_command_poll_timer()
	_write_editor_engine_files()


func _exit_tree() -> void:
	if _heartbeat_timer != null:
		_heartbeat_timer.stop()
		_heartbeat_timer.queue_free()
		_heartbeat_timer = null
	if _command_poll_timer != null:
		_command_poll_timer.stop()
		_command_poll_timer.queue_free()
		_command_poll_timer = null
	_stop_autoload_timer()
	remove_tool_menu_item(MENU_ITEM)


func _start_heartbeat_timer() -> void:
	if _heartbeat_timer != null:
		return

	_heartbeat_timer = Timer.new()
	_heartbeat_timer.wait_time = HEARTBEAT_INTERVAL_SECONDS
	_heartbeat_timer.one_shot = false
	_heartbeat_timer.autostart = true
	_heartbeat_timer.timeout.connect(_write_editor_engine_files)
	add_child(_heartbeat_timer)


func _start_command_poll_timer() -> void:
	if _command_poll_timer != null:
		return

	_command_poll_timer = Timer.new()
	_command_poll_timer.wait_time = COMMAND_POLL_INTERVAL_SECONDS
	_command_poll_timer.one_shot = false
	_command_poll_timer.autostart = true
	_command_poll_timer.timeout.connect(_poll_editor_commands)
	add_child(_command_poll_timer)


func _schedule_godot_bootstrap_autoload() -> void:
	if _ensure_godot_bootstrap_autoload():
		return
	if _autoload_timer != null:
		return

	_autoload_retry_count = 0
	_autoload_timer = Timer.new()
	_autoload_timer.wait_time = AUTOLOAD_RETRY_INTERVAL_SECONDS
	_autoload_timer.one_shot = false
	_autoload_timer.autostart = false
	_autoload_timer.timeout.connect(_retry_godot_bootstrap_autoload)
	add_child(_autoload_timer)
	_autoload_timer.start()


func _retry_godot_bootstrap_autoload() -> void:
	if _ensure_godot_bootstrap_autoload():
		_stop_autoload_timer()
		return

	_autoload_retry_count += 1
	if _autoload_retry_count >= AUTOLOAD_MAX_RETRY_COUNT:
		_stop_autoload_timer()
		push_warning("[YokiFrame] GodotBootstrap is not compiled yet. Rebuild C# or reopen the project to finish autoload registration.")


func _stop_autoload_timer() -> void:
	if _autoload_timer == null:
		return

	_autoload_timer.stop()
	_autoload_timer.queue_free()
	_autoload_timer = null


func _shortcut_input(event: InputEvent) -> void:
	if _is_open_shortcut(event):
		_open_panel()
		get_viewport().set_input_as_handled()


func _is_open_shortcut(event: InputEvent) -> bool:
	if not event is InputEventKey:
		return false

	var key_event: InputEventKey = event as InputEventKey
	return key_event.pressed \
		and not key_event.echo \
		and key_event.ctrl_pressed \
		and not key_event.alt_pressed \
		and not key_event.shift_pressed \
		and key_event.keycode == KEY_E


func _open_panel() -> void:
	var package_root: String = _resolve_package_root()
	if package_root.is_empty():
		push_error("[YokiFrame] Cannot find package root. Reopen the project or copy YokiFrame again.")
		return

	_ensure_addons_entrypoint()
	var runtime_dir: String = ProjectSettings.globalize_path(
		package_root.path_join("TauriRuntime~")
	)
	var binary_path: String = _resolve_tauri_binary_path(runtime_dir)
	var dist_path: String = runtime_dir.path_join("dist")
	var yokiframe_dir: String = ProjectSettings.globalize_path("res://").path_join(".yokiframe")

	if not FileAccess.file_exists(binary_path):
		push_error("[YokiFrame] Tauri binary missing: " + binary_path)
		return
	if not DirAccess.dir_exists_absolute(dist_path):
		push_error("[YokiFrame] Tauri dist missing: " + dist_path)
		return

	DirAccess.make_dir_recursive_absolute(yokiframe_dir)
	_write_editor_engine_files()
	if _is_running_from_pid_file(yokiframe_dir):
		_write_panel_show_request(yokiframe_dir)
		print("[YokiFrame] Panel already running; show requested.")
		return

	var previous_bridge: Dictionary = _capture_environment(YOKI_YOKIFRAME_DIR)
	var previous_dist: Dictionary = _capture_environment(YOKI_DIST_PATH)
	var previous_owner: Dictionary = _capture_environment(YOKI_OWNER_HWND)
	OS.set_environment(YOKI_YOKIFRAME_DIR, yokiframe_dir)
	OS.set_environment(YOKI_DIST_PATH, dist_path)
	var owner_hwnd: int = _resolve_owner_hwnd()
	if owner_hwnd > 0:
		OS.set_environment(YOKI_OWNER_HWND, str(owner_hwnd))
	else:
		OS.unset_environment(YOKI_OWNER_HWND)
	var pid: int = OS.create_process(binary_path, PackedStringArray(), false)
	_restore_environment(YOKI_YOKIFRAME_DIR, previous_bridge)
	_restore_environment(YOKI_DIST_PATH, previous_dist)
	_restore_environment(YOKI_OWNER_HWND, previous_owner)

	if pid <= 0:
		push_error("[YokiFrame] Failed to launch Tauri panel: " + binary_path)
		return

	_write_pid_file(yokiframe_dir, pid)
	_write_panel_show_request(yokiframe_dir)
	print("[YokiFrame] Panel launched. PID: " + str(pid))


func _resolve_tauri_binary_path(runtime_dir: String) -> String:
	var os_name: String = OS.get_name()
	if os_name == "Windows":
		return runtime_dir.path_join("yokiframe-tauri-editor.exe")
	if os_name == "macOS":
		return runtime_dir.path_join("yokiframe-tauri-editor.app").path_join("Contents/MacOS/yokiframe-tauri-editor")
	if os_name == "Linux":
		return runtime_dir.path_join("yokiframe-tauri-editor")
	return runtime_dir.path_join("yokiframe-tauri-editor")


func _resolve_package_root() -> String:
	var configured: String = str(ProjectSettings.get_setting(PACKAGE_ROOT_SETTING, ""))
	if _is_valid_package_root(configured):
		return configured

	var plugin_path: String = str(get_script().resource_path)
	var marker: String = "/" + REAL_PLUGIN_RELATIVE_PATH
	var marker_index: int = plugin_path.find(marker)
	if marker_index > 0:
		var script_root: String = plugin_path.substr(0, marker_index)
		if _is_valid_package_root(script_root):
			return script_root

	var default_root: String = _find_default_package_root()
	if not default_root.is_empty():
		return default_root

	return ""


func _resolve_owner_hwnd() -> int:
	if OS.get_name() != "Windows":
		return 0

	var handle: int = DisplayServer.window_get_native_handle(DisplayServer.WINDOW_HANDLE, DisplayServer.MAIN_WINDOW_ID)
	if handle <= 0:
		push_warning("[YokiFrame] Cannot resolve Godot editor HWND; Tauri panel will launch as an independent window.")
		return 0

	return handle


func _is_valid_package_root(package_root: String) -> bool:
	if package_root.is_empty():
		return false

	var real_plugin_path: String = package_root.path_join(REAL_PLUGIN_RELATIVE_PATH)
	var bootstrap_path: String = package_root.path_join(BOOTSTRAP_RELATIVE_PATH)
	return FileAccess.file_exists(real_plugin_path) and FileAccess.file_exists(bootstrap_path)


func _ensure_godot_bootstrap_autoload() -> bool:
	var package_root: String = _resolve_package_root()
	if package_root.is_empty():
		return false

	var bootstrap_path: String = package_root.path_join(BOOTSTRAP_RELATIVE_PATH)
	var changed: bool = false
	changed = _set_project_setting_if_changed(PACKAGE_ROOT_SETTING, package_root) or changed
	if not _is_bootstrap_script_ready(bootstrap_path):
		if changed:
			_save_project_settings("[YokiFrame] Cannot save Godot package root: ")
		return false

	changed = _set_project_setting_if_changed(AUTOLOAD_SETTING, "*" + bootstrap_path) or changed
	if changed:
		_save_project_settings("[YokiFrame] Cannot save Godot bootstrap registration: ")

	return true


func _is_bootstrap_script_ready(bootstrap_path: String) -> bool:
	var assembly_path: String = _resolve_csharp_assembly_path()
	if assembly_path.is_empty():
		return false

	var bootstrap_absolute_path: String = ProjectSettings.globalize_path(bootstrap_path)
	var assembly_modified_time: int = int(FileAccess.get_modified_time(assembly_path))
	var bootstrap_modified_time: int = int(FileAccess.get_modified_time(bootstrap_absolute_path))
	if assembly_modified_time <= 0 or bootstrap_modified_time <= 0:
		return false
	if assembly_modified_time < bootstrap_modified_time:
		return false

	var bootstrap_script: Resource = load(bootstrap_path)
	if bootstrap_script == null or not bootstrap_script is Script:
		return false

	var script: Script = bootstrap_script as Script
	return script.can_instantiate()


func _resolve_csharp_assembly_path() -> String:
	var assembly_name: String = str(ProjectSettings.get_setting(DOTNET_ASSEMBLY_NAME_SETTING, ""))
	if assembly_name.is_empty():
		return ""

	var project_root: String = ProjectSettings.globalize_path("res://")
	var debug_path: String = project_root.path_join(".godot/mono/temp/bin/Debug").path_join(assembly_name + ".dll")
	if FileAccess.file_exists(debug_path):
		return debug_path

	var release_path: String = project_root.path_join(".godot/mono/temp/bin/Release").path_join(assembly_name + ".dll")
	if FileAccess.file_exists(release_path):
		return release_path

	return ""


func _set_project_setting_if_changed(key: String, value: String) -> bool:
	if ProjectSettings.has_setting(key):
		var current_value: String = str(ProjectSettings.get_setting(key))
		if current_value == value:
			return false

	ProjectSettings.set_setting(key, value)
	return true


func _save_project_settings(error_prefix: String) -> bool:
	var save_error: int = ProjectSettings.save()
	if save_error != OK:
		push_warning(error_prefix + str(save_error))
		return false

	return true


func _find_default_package_root() -> String:
	if _is_valid_package_root(INSTALLER_PACKAGE_ROOT):
		return INSTALLER_PACKAGE_ROOT

	if _is_valid_package_root(DEFAULT_PACKAGE_ROOT):
		return DEFAULT_PACKAGE_ROOT

	return ""


func _ensure_addons_entrypoint() -> void:
	var package_root: String = _resolve_package_root()
	if package_root.is_empty():
		return

	var target_dir: String = ProjectSettings.globalize_path(ADDONS_PLUGIN_DIR)
	DirAccess.make_dir_recursive_absolute(target_dir)
	_write_text_if_changed(ADDONS_PLUGIN_CONFIG_PATH, _build_addons_plugin_config())
	_write_text_if_changed(ADDONS_PLUGIN_SCRIPT_PATH, _build_addons_plugin_stub(package_root))
	_ensure_addons_plugin_enabled()
	_refresh_addons_entrypoint_files()


func _build_addons_plugin_config() -> String:
	return "[plugin]\n" \
		+ "\n" \
		+ "name=\"YokiFrame\"\n" \
		+ "description=\"YokiFrame editor panel launcher\"\n" \
		+ "author=\"YokiFrame\"\n" \
		+ "version=\"" + ADAPTER_VERSION + "\"\n" \
		+ "script=\"plugin.gd\"\n"


func _build_addons_plugin_stub(package_root: String) -> String:
	var normalized_root: String = package_root
	while normalized_root.ends_with("/"):
		normalized_root = normalized_root.substr(0, normalized_root.length() - 1)

	var real_plugin_path: String = normalized_root.path_join(REAL_PLUGIN_RELATIVE_PATH)
	return "@tool\n" \
		+ "extends \"" + _escape_gd_string(real_plugin_path) + "\"\n"


func _ensure_addons_plugin_enabled() -> void:
	var enabled_plugins: PackedStringArray = PackedStringArray()
	if ProjectSettings.has_setting(EDITOR_PLUGINS_ENABLED_SETTING):
		var current_value: Variant = ProjectSettings.get_setting(EDITOR_PLUGINS_ENABLED_SETTING)
		if current_value is PackedStringArray:
			enabled_plugins = current_value
		elif current_value is Array:
			for value in current_value:
				enabled_plugins.append(str(value))

	for i in range(enabled_plugins.size()):
		if str(enabled_plugins[i]) == ADDONS_PLUGIN_CONFIG_PATH:
			return

	enabled_plugins.append(ADDONS_PLUGIN_CONFIG_PATH)
	ProjectSettings.set_setting(EDITOR_PLUGINS_ENABLED_SETTING, enabled_plugins)
	var save_error: int = ProjectSettings.save()
	if save_error != OK:
		push_warning("[YokiFrame] Cannot save Godot plugin registration: " + str(save_error))


func _refresh_addons_entrypoint_files() -> void:
	var editor_filesystem: EditorFileSystem = get_editor_interface().get_resource_filesystem()
	if editor_filesystem == null:
		return

	editor_filesystem.update_file(ADDONS_PLUGIN_CONFIG_PATH)
	editor_filesystem.update_file(ADDONS_PLUGIN_SCRIPT_PATH)


func _write_editor_engine_files() -> void:
	var yokiframe_dir: String = _resolve_yokiframe_dir()
	var engine_dir: String = yokiframe_dir.path_join("engines").path_join(GODOT_EDITOR_ENGINE_ID)
	var status_dir: String = engine_dir.path_join("status")
	DirAccess.make_dir_recursive_absolute(status_dir)

	var engine_path: String = engine_dir.path_join("engine.json")
	var heartbeat_path: String = status_dir.path_join("heartbeat.json")
	_atomic_write_text(engine_path, _build_engine_registry_json())
	_atomic_write_text(heartbeat_path, _build_heartbeat_json())


func _write_panel_show_request(yokiframe_dir: String) -> void:
	var panel_dir: String = yokiframe_dir.path_join(PANEL_REQUEST_DIR)
	DirAccess.make_dir_recursive_absolute(panel_dir)
	var request_path: String = panel_dir.path_join(PANEL_SHOW_REQUEST_FILE)
	_atomic_write_text(request_path, _build_panel_show_request_json())
	print("[YokiFrame] Panel show requested: " + request_path)


func _poll_editor_commands() -> void:
	var yokiframe_dir: String = _resolve_yokiframe_dir()
	_poll_command_dir(yokiframe_dir.path_join("engines").path_join(GODOT_EDITOR_ENGINE_ID))
	_poll_command_dir(yokiframe_dir)


func _poll_command_dir(root_dir: String) -> void:
	var command_dir: String = root_dir.path_join("commands")
	if not DirAccess.dir_exists_absolute(command_dir):
		return

	var dir: DirAccess = DirAccess.open(command_dir)
	if dir == null:
		return

	dir.list_dir_begin()
	while true:
		var file_name: String = dir.get_next()
		if file_name.is_empty():
			break
		if dir.current_is_dir() or not file_name.ends_with(".json"):
			continue

		var command_path: String = command_dir.path_join(file_name)
		_handle_editor_command_file(root_dir, command_path)
	dir.list_dir_end()


func _handle_editor_command_file(root_dir: String, command_path: String) -> void:
	var command_text: String = FileAccess.get_file_as_string(command_path)
	if command_text.is_empty():
		return

	var parsed: Variant = JSON.parse_string(command_text)
	var request_id: String = command_path.get_file().get_basename()
	var response_json: String
	if parsed is Dictionary:
		var command: Dictionary = parsed as Dictionary
		request_id = _safe_request_id(str(command.get("requestId", request_id)), request_id)
		response_json = _handle_editor_command(command, request_id)
	else:
		response_json = _build_error_response(request_id, "System", "unknown_response", "InvalidCommandJson", "Command JSON is invalid.", false)

	var results_dir: String = root_dir.path_join("results")
	DirAccess.make_dir_recursive_absolute(results_dir)
	_atomic_write_text(results_dir.path_join(request_id + "-response.json"), response_json)
	DirAccess.remove_absolute(command_path)


func _handle_editor_command(command: Dictionary, request_id: String) -> String:
	var kit: String = str(command.get("kit", ""))
	var action: String = str(command.get("action", ""))
	var engine_id: String = str(command.get("engineId", GODOT_EDITOR_ENGINE_ID))

	if not engine_id.is_empty() and engine_id != GODOT_EDITOR_ENGINE_ID:
		return _build_error_response(request_id, kit, action + "_response", "EngineIdMismatch", "Command engineId does not match the Godot editor host.", false)
	if kit != "System":
		return _build_error_response(request_id, kit, action + "_response", "UnknownKit", "Godot editor host only supports System commands. Start the Godot runtime bridge for Kit commands.", false)

	match action:
		"ping":
			return _build_success_response(request_id, kit, "ping_response", "{\"message\":\"pong\",\"hostKind\":\"editor\"}")
		"status":
			return _build_success_response(request_id, kit, "status_response", _build_editor_status_data_json())
		"bridge_status":
			return _build_success_response(request_id, kit, "bridge_status_response", _build_editor_bridge_status_json())
		"list_commands":
			return _build_success_response(request_id, kit, "list_commands_response", _build_editor_command_catalog_json())
		_:
			return _build_error_response(request_id, kit, action + "_response", "UnknownAction", "Godot editor host does not support System/" + action + ".", false)


func _resolve_yokiframe_dir() -> String:
	return ProjectSettings.globalize_path("res://").path_join(".yokiframe")


func _build_engine_registry_json() -> String:
	var luban_available: String = _json_bool(_is_luban_environment_available())
	# implementedKits 表示 Godot 包侧已提供的用户可见 Kit；具体命令/静态扫描能力由 kitFeatures 区分。
	return "{\"protocolVersion\":2" \
		+ ",\"engineId\":\"" + GODOT_EDITOR_ENGINE_ID + "\"" \
		+ ",\"engine\":\"Godot\"" \
		+ ",\"hostKind\":\"editor\"" \
		+ ",\"version\":\"" + _json_escape(_get_godot_version()) + "\"" \
		+ ",\"projectPath\":\"" + _json_escape(ProjectSettings.globalize_path("res://")) + "\"" \
		+ ",\"adapterVersion\":\"" + ADAPTER_VERSION + "\"" \
		+ ",\"startedAtUtc\":\"" + _json_escape(_started_at_utc) + "\"" \
		+ ",\"capabilities\":[\"commands\",\"heartbeat\",\"bridge_status\",\"static_scan\"]" \
		+ ",\"implementedKits\":" + GODOT_IMPLEMENTED_KITS_JSON \
		+ ",\"kitFeatures\":" + GODOT_EDITOR_KIT_FEATURES_JSON \
		+ ",\"optionalDependencies\":{\"luban\":{\"available\":" + luban_available \
		+ ",\"define\":\"" + LUBAN_SUPPORT_DEFINE + "\"" \
		+ ",\"packageName\":\"" + LUBAN_PACKAGE_NAME + "\"" \
		+ ",\"asmdefName\":\"" + LUBAN_RUNTIME_ASSEMBLY_NAME + "\"" \
		+ ",\"typeName\":\"" + LUBAN_RUNTIME_TYPE_NAME + "\"}}}"


func _is_luban_environment_available() -> bool:
	var project_root: String = ProjectSettings.globalize_path("res://")
	if project_root.is_empty():
		return false

	if _file_contains_text(project_root.path_join(DIRECTORY_BUILD_PROPS_FILE), LUBAN_RUNTIME_ASSEMBLY_NAME):
		return true

	var dir: DirAccess = DirAccess.open(project_root)
	if dir == null:
		return false

	dir.list_dir_begin()
	while true:
		var file_name: String = dir.get_next()
		if file_name.is_empty():
			break
		if dir.current_is_dir():
			continue
		if file_name.ends_with(".csproj") or file_name.ends_with(".props"):
			if _file_contains_text(project_root.path_join(file_name), LUBAN_RUNTIME_ASSEMBLY_NAME):
				dir.list_dir_end()
				return true
	dir.list_dir_end()

	return FileAccess.file_exists(project_root.path_join(LUBAN_RUNTIME_ASSEMBLY_NAME + ".dll"))


func _file_contains_text(path: String, needle: String) -> bool:
	if not FileAccess.file_exists(path):
		return false

	var content: String = FileAccess.get_file_as_string(path)
	return content.to_lower().find(needle.to_lower()) >= 0


func _build_success_response(request_id: String, kit: String, action: String, data_json: String) -> String:
	return "{\"protocolVersion\":2" \
		+ ",\"engineId\":\"" + GODOT_EDITOR_ENGINE_ID + "\"" \
		+ ",\"requestId\":\"" + _json_escape(request_id) + "\"" \
		+ ",\"status\":\"success\"" \
		+ ",\"kit\":\"" + _json_escape(kit) + "\"" \
		+ ",\"action\":\"" + _json_escape(action) + "\"" \
		+ ",\"timestamp\":\"" + _json_escape(_utc_now_string()) + "\"" \
		+ ",\"completedAtUtc\":\"" + _json_escape(_utc_now_string()) + "\"" \
		+ ",\"data\":" + data_json \
		+ "}"


func _build_error_response(request_id: String, kit: String, action: String, code: String, message: String, recoverable: bool) -> String:
	var recoverable_text: String = "true" if recoverable else "false"
	return "{\"protocolVersion\":2" \
		+ ",\"engineId\":\"" + GODOT_EDITOR_ENGINE_ID + "\"" \
		+ ",\"requestId\":\"" + _json_escape(request_id) + "\"" \
		+ ",\"status\":\"error\"" \
		+ ",\"kit\":\"" + _json_escape(kit) + "\"" \
		+ ",\"action\":\"" + _json_escape(action) + "\"" \
		+ ",\"timestamp\":\"" + _json_escape(_utc_now_string()) + "\"" \
		+ ",\"completedAtUtc\":\"" + _json_escape(_utc_now_string()) + "\"" \
		+ ",\"error\":{\"code\":\"" + _json_escape(code) + "\",\"message\":\"" + _json_escape(message) + "\",\"recoverable\":" + recoverable_text + "}" \
		+ ",\"errorMessage\":\"" + _json_escape(message) + "\"" \
		+ "}"


func _build_editor_status_data_json() -> String:
	var uptime: int = max(0, int(Time.get_unix_time_from_system()) - _parse_started_at_unix_seconds())
	return "{\"engine\":\"Godot\",\"hostKind\":\"editor\",\"version\":\"" + _json_escape(_get_godot_version()) + "\",\"uptime\":" + str(uptime) + "}"


func _build_editor_bridge_status_json() -> String:
	var yokiframe_dir: String = _resolve_yokiframe_dir()
	var command_count: int = _count_json_files(yokiframe_dir.path_join("engines").path_join(GODOT_EDITOR_ENGINE_ID).path_join("commands"))
	var result_count: int = _count_json_files(yokiframe_dir.path_join("engines").path_join(GODOT_EDITOR_ENGINE_ID).path_join("results"))
	return "{\"protocolVersion\":2" \
		+ ",\"engineId\":\"" + GODOT_EDITOR_ENGINE_ID + "\"" \
		+ ",\"pendingCommandCount\":" + str(command_count) \
		+ ",\"processingCommandCount\":0" \
		+ ",\"deadletterCommandCount\":0" \
		+ ",\"resultCount\":" + str(result_count) \
		+ ",\"bridgeBusyCount\":0" \
		+ ",\"backpressureActive\":false" \
		+ "}"


func _build_editor_command_catalog_json() -> String:
	return "{\"kits\":[{\"kit\":\"System\",\"actions\":[{\"action\":\"ping\"},{\"action\":\"status\"},{\"action\":\"bridge_status\"},{\"action\":\"list_commands\"}]}]}"


func _safe_request_id(value: String, fallback: String) -> String:
	if _is_safe_identifier(value):
		return value
	if _is_safe_identifier(fallback):
		return fallback
	return "godot-editor-invalid-request"


func _is_safe_identifier(value: String) -> bool:
	if value.is_empty() or value.length() > 128 or value == "." or value == "..":
		return false

	for i in range(value.length()):
		var code: int = value.unicode_at(i)
		var is_digit: bool = code >= 48 and code <= 57
		var is_upper: bool = code >= 65 and code <= 90
		var is_lower: bool = code >= 97 and code <= 122
		var is_symbol: bool = code == 45 or code == 46 or code == 95
		if not (is_digit or is_upper or is_lower or is_symbol):
			return false

	return true


func _count_json_files(dir_path: String) -> int:
	if not DirAccess.dir_exists_absolute(dir_path):
		return 0

	var dir: DirAccess = DirAccess.open(dir_path)
	if dir == null:
		return 0

	var count: int = 0
	dir.list_dir_begin()
	while true:
		var file_name: String = dir.get_next()
		if file_name.is_empty():
			break
		if not dir.current_is_dir() and file_name.ends_with(".json"):
			count += 1
	dir.list_dir_end()
	return count


func _parse_started_at_unix_seconds() -> int:
	if _started_at_utc.is_empty():
		return int(Time.get_unix_time_from_system())

	var text: String = _started_at_utc
	if text.ends_with("Z"):
		text = text.substr(0, text.length() - 1)

	var parsed: Dictionary = Time.get_datetime_dict_from_datetime_string(text, false)
	if parsed.is_empty():
		return int(Time.get_unix_time_from_system())

	return int(Time.get_unix_time_from_datetime_dict(parsed))


func _build_heartbeat_json() -> String:
	return "{\"protocolVersion\":2" \
		+ ",\"engineId\":\"" + GODOT_EDITOR_ENGINE_ID + "\"" \
		+ ",\"timestamp\":" + str(int(Time.get_unix_time_from_system())) \
		+ ",\"createdAtUtc\":\"" + _json_escape(_utc_now_string()) + "\"}"


func _build_panel_show_request_json() -> String:
	return "{\"protocolVersion\":2" \
		+ ",\"type\":\"show_window\"" \
		+ ",\"activate\":true" \
		+ ",\"source\":\"" + GODOT_EDITOR_ENGINE_ID + "\"" \
		+ ",\"createdAtUtc\":\"" + _json_escape(_utc_now_string()) + "\"}"


func _get_godot_version() -> String:
	var version_info: Dictionary = Engine.get_version_info()
	if version_info.has("string"):
		return str(version_info["string"])

	return ""


func _utc_now_string() -> String:
	return Time.get_datetime_string_from_system(true, false) + "Z"


func _json_escape(value: String) -> String:
	return value \
		.replace("\\", "\\\\") \
		.replace("\"", "\\\"") \
		.replace("\n", "\\n") \
		.replace("\r", "\\r") \
		.replace("\t", "\\t")


func _json_bool(value: bool) -> String:
	return "true" if value else "false"


func _escape_gd_string(value: String) -> String:
	return value \
		.replace("\\", "\\\\") \
		.replace("\"", "\\\"")


func _write_text_if_changed(path: String, content: String) -> void:
	if FileAccess.file_exists(path):
		var current_content: String = FileAccess.get_file_as_string(path)
		if current_content == content:
			return

	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		push_warning("[YokiFrame] Cannot write generated plugin entrypoint: " + path)
		return

	file.store_string(content)
	file.flush()


func _atomic_write_text(path: String, content: String) -> void:
	var temp_path: String = path + ".tmp"
	var file: FileAccess = FileAccess.open(temp_path, FileAccess.WRITE)
	if file == null:
		push_warning("[YokiFrame] Cannot write temp file: " + temp_path)
		return

	file.store_string(content)
	file.flush()
	file = null

	if FileAccess.file_exists(path):
		var remove_error: int = DirAccess.remove_absolute(path)
		if remove_error != OK:
			push_warning("[YokiFrame] Cannot replace existing protocol file: " + path)
			DirAccess.remove_absolute(temp_path)
			return

	var rename_error: int = DirAccess.rename_absolute(temp_path, path)
	if rename_error != OK:
		push_warning("[YokiFrame] Cannot commit protocol file: " + path)
		DirAccess.remove_absolute(temp_path)


func _capture_environment(name: String) -> Dictionary:
	return {
		"exists": OS.has_environment(name),
		"value": OS.get_environment(name)
	}


func _restore_environment(name: String, snapshot: Dictionary) -> void:
	if snapshot.get("exists", false):
		OS.set_environment(name, str(snapshot.get("value", "")))
	else:
		OS.unset_environment(name)


func _is_running_from_pid_file(yokiframe_dir: String) -> bool:
	var pid_path: String = yokiframe_dir.path_join(PID_FILE)
	if not FileAccess.file_exists(pid_path):
		return false

	var pid_text: String = FileAccess.get_file_as_string(pid_path).strip_edges()
	if not pid_text.is_valid_int():
		return false

	var pid: int = int(pid_text)
	return pid > 0 and OS.is_process_running(pid)


func _write_pid_file(yokiframe_dir: String, pid: int) -> void:
	var pid_path: String = yokiframe_dir.path_join(PID_FILE)
	var file: FileAccess = FileAccess.open(pid_path, FileAccess.WRITE)
	if file == null:
		push_warning("[YokiFrame] Cannot write panel pid file: " + pid_path)
		return

	file.store_string(str(pid))
