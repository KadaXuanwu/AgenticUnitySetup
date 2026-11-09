/* __GAME__: after every bank build, from FMOD Studio or fmodstudiocl, write the project's events, snapshots,
   buses, VCAs and parameters as C# names into Unity/Assets/__GAME__/Runtime/Audio/Fmod.cs. Code never spells a
   path, and renaming or deleting an event the game plays fails to compile instead of failing silent. */

(function () {
    var OUTPUT = "/../../Unity/Assets/__GAME__/Runtime/Audio/Fmod.cs";

    // Names that a generated member must not take: Ref is the helper every field calls, and C# forbids a member
    // with the name of the class it is in.
    var RESERVED = ["Ref"];

    function identifier(name) {
        var words = name.replace(/[^A-Za-z0-9]+/g, " ").trim().split(" ");
        var id = words.map(function (w) { return w.charAt(0).toUpperCase() + w.slice(1); }).join("");
        return /^[0-9]/.test(id) ? "_" + id : id || "_";
    }

    // One C# class body. Two FMOD names can give the same identifier ("Hit 1" and "Hit-1"), or the identifier can
    // be the class's own name: the later one gets a number, and the file says so in a comment.
    function scope(className, collisions) {
        var used = {};
        used[className] = true;
        RESERVED.forEach(function (r) { used[r] = true; });
        return function (name, source) {
            var base = identifier(name);
            var id = base;
            for (var n = 2; used.hasOwnProperty(id); n++) {
                id = base + "_" + n;
            }
            used[id] = true;
            if (id !== base) {
                collisions.push(source + " is " + id + ": " + base + " was taken.");
            }
            return id;
        };
    }

    function csString(text) {
        return "\"" + String(text).replace(/\\/g, "\\\\").replace(/"/g, "\\\"") + "\"";
    }

    function byName(a, b) { return a.name < b.name ? -1 : a.name > b.name ? 1 : 0; }

    function byPath(a, b) { return a.getPath() < b.getPath() ? -1 : a.getPath() > b.getPath() ? 1 : 0; }

    function referenceLine(indent, id, object) {
        return indent + "public static readonly global::FMODUnity.EventReference " + id +
            " = Ref(" + csString(object.id) + ", " + csString(object.getPath()) + ");";
    }

    // An event folder has no getPath(), so the folder path comes down from the caller ("event:/Weapons/").
    function eventLines(folder, path, className, indent, out, collisions) {
        var name = scope(className, collisions);
        folder.items.slice().sort(byName).forEach(function (item) {
            if (item.isOfType("EventFolder")) {
                var id = name(item.name, "Folder " + path + item.name);
                // Read-only names: nothing to reset on entering play mode.
                out.push(indent + "[NoAutoStaticsCleanup]");
                out.push(indent + "public static class " + id + " {");
                eventLines(item, path + item.name + "/", id, indent + "    ", out, collisions);
                out.push(indent + "}");
            } else if (item.isOfExactType("Event")) {
                out.push(referenceLine(indent, name(item.name, item.getPath()), item));
            }
        });
    }

    function constants(title, objects, key, value, out, collisions) {
        var name = scope(title, collisions);
        out.push("        public static class " + title + " {");
        objects.forEach(function (o) {
            out.push("            public const string " + name(key(o), value(o)) + " = " + csString(value(o)) + ";");
        });
        out.push("        }");
        out.push("");
    }

    function generate() {
        var p = studio.project;
        var model = p.model;
        var collisions = [];
        var body = [];

        body.push("        [NoAutoStaticsCleanup]");
        body.push("        public static class Events {");
        eventLines(p.workspace.masterEventFolder, "event:/", "Events", "            ", body, collisions);
        body.push("        }");
        body.push("");

        var snapshot = scope("Snapshots", collisions);
        body.push("        [NoAutoStaticsCleanup]");
        body.push("        public static class Snapshots {");
        model.Snapshot.findInstances().sort(byPath).forEach(function (s) {
            body.push(referenceLine("            ", snapshot(s.name, s.getPath()), s));
        });
        body.push("        }");
        body.push("");

        var buses = model.MixerGroup.findInstances().concat(model.MixerReturn.findInstances());
        buses = buses.filter(function (b) { return b.getPath().indexOf("bus:/") === 0; }).sort(byPath);
        var bus = scope("Buses", collisions);
        bus("Master", "bus:/");
        body.push("        public static class Buses {");
        body.push("            public const string Master = \"bus:/\";");
        buses.forEach(function (b) {
            body.push("            public const string " + bus(b.getPath().substring(5), b.getPath()) + " = " +
                csString(b.getPath()) + ";");
        });
        body.push("        }");
        body.push("");

        constants("Vcas", model.MixerVCA.findInstances().sort(byPath),
            function (v) { return v.name; }, function (v) { return v.getPath(); }, body, collisions);
        constants("Parameters", model.ParameterPreset.findInstances().sort(byName),
            function (pr) { return pr.name; }, function (pr) { return pr.name; }, body, collisions);

        var out = ["// Generated by Audio/__GAME__/Scripts/names.js on every FMOD bank build. Do not edit."];
        collisions.forEach(function (c) { out.push("// Name collision: " + c); });
        out = out.concat([
            "using Unity.Scripting.LifecycleManagement;",
            "",
            "namespace __GAME__.Runtime.Audio {",
            "    public static class Fmod {"
        ]).concat(body).concat([
            "        private static global::FMODUnity.EventReference Ref(string guid, string path) {",
            "            global::FMODUnity.EventReference reference = new global::FMODUnity.EventReference {",
            "                Guid = global::FMOD.GUID.Parse(guid)",
            "            };",
            "#if UNITY_EDITOR",
            "            reference.Path = path;",
            "#endif",
            "            return reference;",
            "        }",
            "    }",
            "}"
        ]);
        var text = out.join("\n") + "\n";

        collisions.forEach(function (c) { console.warn("__GAME__: FMOD name collision: " + c); });

        var path = p.filePath.substr(0, p.filePath.lastIndexOf("/")) + OUTPUT;
        var file = studio.system.getFile(path);
        if (file.exists() && file.open(studio.system.openMode.ReadOnly)) {
            var old = file.readText(1 << 20);
            file.close();
            if (old === text) {
                return;
            }
        }
        if (!file.open(studio.system.openMode.WriteOnly)) {
            console.error("__GAME__: cannot write " + path);
            return;
        }
        file.writeText(text);
        file.close();
        console.log("__GAME__: wrote " + path);
    }

    studio.project.buildEndedConnect(function (success) {
        if (success) {
            generate();
        }
    });
})();
