extends SceneTree
# Disposable EMPTY project, never mod/: source files must not hide a broken export.

func _initialize() -> void:
    var args := OS.get_cmdline_user_args()
    if args.size() != 3:
        push_error("Expected PCK, required-resource JSON, report JSON")
        quit(2)
        return
    var failures: Array[String] = []
    var checked := 0
    if not ProjectSettings.load_resource_pack(args[0], true):
        failures.append("PCK could not be mounted")
    else:
        var required = JSON.parse_string(FileAccess.get_file_as_string(args[1]))
        if not required is Array or required.is_empty():
            failures.append("Required resource list missing/invalid")
        else:
            for entry in required:
                checked += 1
                var path: String = entry["path"]
                var kind: String = entry["kind"]
                if kind == "texture":
                    var texture = ResourceLoader.load(path, "Texture2D")
                    if not texture is Texture2D or texture.get_width() <= 0 or texture.get_height() <= 0:
                        failures.append("Texture did not load: " + path)
                elif kind == "audio":
                    if not ResourceLoader.load(path) is AudioStream:
                        failures.append("Audio did not load: " + path)
                else:
                    if not FileAccess.file_exists(path):
                        failures.append("File missing: " + path)
                    else:
                        var content := FileAccess.get_file_as_string(path)
                        if content.is_empty():
                            failures.append("File empty: " + path)
                        elif kind == "json":
                            var json := JSON.new()
                            if json.parse(content) != OK:
                                failures.append("Invalid JSON: " + path)
                        elif kind == "csv":
                            var csv := FileAccess.open(path, FileAccess.READ)
                            var columns := 0
                            var rows := 0
                            while csv != null and not csv.eof_reached():
                                var fields := csv.get_csv_line()
                                if fields.is_empty() or fields[0].strip_edges().is_empty() or fields[0].begins_with("#"):
                                    continue
                                if columns == 0:
                                    columns = fields.size()
                                elif columns != fields.size():
                                    failures.append("CSV column count mismatch: " + path)
                                    break
                                else:
                                    rows += 1
                            if csv != null:
                                csv.close()
                            if columns == 0 or rows == 0:
                                failures.append("CSV has no header/data: " + path)
    var report := {"checked": checked, "failed": failures, "isolated_project": true,
                   "engine": Engine.get_version_info().get("string", "unknown"), "game_runtime": false}
    var file := FileAccess.open(args[2], FileAccess.WRITE)
    if file == null:
        push_error("Could not write pack verification report")
        quit(3)
        return
    file.store_string(JSON.stringify(report, "  "))
    file.close()
    print(JSON.stringify(report))
    quit(0 if failures.is_empty() and checked > 0 else 1)
