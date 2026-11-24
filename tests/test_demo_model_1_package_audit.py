import importlib.util
import os
import tempfile
import unittest
import zipfile

SPEC = importlib.util.spec_from_file_location("audit_demo", os.path.join(os.path.dirname(__file__), "..", "tools", "audit_demo_model_1_package.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class DemoPackageAuditTests(unittest.TestCase):
    def make_zip(self, entries):
        temporary = tempfile.NamedTemporaryFile(suffix=".zip", delete=False)
        temporary.close()
        with zipfile.ZipFile(temporary.name, "w") as archive:
            for name, content in entries.items():
                archive.writestr(name, content)
        self.addCleanup(lambda: os.path.exists(temporary.name) and os.unlink(temporary.name))
        return temporary.name

    def test_accepts_valid_minimal_route_archive_without_release_hash(self):
        path = self.make_zip({
            "Demo/ROUTES/Route/Route.trk": b"\\xff\\xfe" + "SIMISA".encode("utf-16le") + "(Tr_RouteFile (RouteID ( R ) Name ( \\"Route name\\" )))".encode("utf-16le"),
            "Demo/ROUTES/Route/Route.tdb": b"tdb",
            "Demo/ROUTES/Route/WORLD/w-000000+000000.w": b"world",
            "Demo/ROUTES/Route/ACTIVITIES/test.act": b"activity",
            "Demo/Global/TSECTION.DAT": b"sections",
        })
        result = MODULE.audit(path, verify_hash=False)
        self.assertEqual(result["route_id"], "R")
        self.assertEqual(result["route_name"], "Route name")
        self.assertEqual(result["activities"], 1)
        self.assertEqual(result["world_tiles"], 1)
        self.assertEqual(result["global_tsection_files"], 1)

    def test_rejects_missing_tdb(self):
        path = self.make_zip({"route/route.trk": b"\\xff\\xfe" + "SIMISA(Tr_RouteFile (RouteID ( R ) Name ( \\"Route name\\" )))".encode("utf-16le")})
        with self.assertRaisesRegex(ValueError, "Missing route TDB"):
            MODULE.audit(path, verify_hash=False)

    def test_rejects_traversal_entry(self):
        path = self.make_zip({"../outside.txt": b"bad", "route/route.trk": b"\\xff\\xfe" + "SIMISA(Tr_RouteFile (RouteID ( R ) Name ( \\"Route name\\" )))".encode("utf-16le"), "route/route.tdb": b"tdb"})
        with self.assertRaisesRegex(ValueError, "Unsafe archive path"):
            MODULE.audit(path, verify_hash=False)


if __name__ == "__main__":
    unittest.main()
