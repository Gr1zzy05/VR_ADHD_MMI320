using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomeTasks.Editor
{
    public static partial class HomeSceneBuilder
    {
        const string ConstructionAudioPath = Root + "/Audio/freesound_community-construction-35446.mp3";

        public static void AddOutdoorDistractions()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.isDirty || scene.path != Root + "/Scenes/NewModel.unity")
                throw new InvalidOperationException("Open the saved NewModel scene in Edit mode.");

            var roots = scene.GetRootGameObjects();
            var router = roots.SelectMany(o => o.GetComponentsInChildren<PlayerModeRouter>(true)).Single();
            var boundary = roots.Single(o => o.name == "Invis_Wall").GetComponent<Collider>();
            var road = roots.Single(o => o.name == "Home").transform.Find("Plane (1)").GetComponent<Renderer>();
            var conversation = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/NeighbourConversation.wav");
            var party = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/NeighbourParty.wav");
            if (conversation == null || party == null || roots.Any(o => o.name == "Outdoor distractions"))
                throw new InvalidOperationException("The new audio must be imported and the outdoor group must not already exist.");

            var outdoor = Group("Outdoor distractions", null, Vector3.zero);
            MakeNeighbourConversation(outdoor, router, conversation);
            MakeNeighbourParty(outdoor, router, party);
            MakeConstructionSite(outdoor, router, road.bounds.min.z - 4.9f, boundary);
            ConfigureOutdoorAudio(outdoor);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var results = EditorSceneManager.OpenScene(HomeSession.ResultsScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var panel in results.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HomeResultsPanel>(true)))
                    ResizeDistractionRows(panel);
                EditorSceneManager.MarkSceneDirty(results);
                EditorSceneManager.SaveScene(results);
            }
            finally
            {
                SceneManager.SetActiveScene(scene);
                EditorSceneManager.CloseScene(results, true);
            }
        }

        public static void UpdateOutdoorAudio()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != Root + "/Scenes/NewModel.unity")
                throw new InvalidOperationException("Open NewModel in Edit mode.");
            var outdoor = scene.GetRootGameObjects().Single(root => root.name == "Outdoor distractions");
            ConfigureOutdoorAudio(outdoor.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("NewModel could not be saved.");
        }

        static void ConfigureOutdoorAudio(Transform outdoor)
        {
            if (AssetImporter.GetAtPath(ConstructionAudioPath) is AudioImporter importer && !importer.forceToMono)
            {
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ConstructionAudioPath);
            if (clip == null) throw new InvalidOperationException("The construction audio clip is missing.");

            var building = outdoor.Find("House sound boundary");
            if (building == null) building = Group("House sound boundary", outdoor, Vector3.zero);
            // The entry and kitchen walls have different setbacks from the street.
            var interiors = new[]
            {
                IndoorAudioBox(building, "Entry and living rooms", new Vector3(-8f, -0.25f, -3.61f), new Vector3(-1.02f, 5.75f, 16.39f)),
                IndoorAudioBox(building, "Kitchen and adjoining rooms", new Vector3(-1.02f, -0.25f, -4.91f), new Vector3(7.6f, 5.75f, 16.39f))
            };

            foreach (var activity in outdoor.GetComponentsInChildren<OutdoorDistraction>(true))
            {
                activity.audioInteriors = interiors;
                var construction = activity.kind == DistractionKind.Construction;
                if (construction)
                {
                    var site = activity.gazeTarget.parent;
                    if (activity.sound == null)
                        activity.sound = site.GetComponentsInChildren<AudioSource>(true).FirstOrDefault(source => source.clip == clip);
                    if (activity.sound == null)
                        activity.sound = OutdoorSound(site, "Construction sound", new Vector3(0, 1.4f, 0.2f), clip, 0.55f, 3200f);
                    activity.sound.clip = clip;
                    activity.sound.volume = 0.55f;
                }
                activity.sound.playOnAwake = false;
                activity.sound.loop = true;
                activity.sound.spatialBlend = 1f;
                activity.sound.dopplerLevel = 0f;
                activity.sound.rolloffMode = AudioRolloffMode.Linear;
                activity.sound.minDistance = construction ? 18f : 4f;
                activity.sound.maxDistance = construction ? 105f : 48f;
                EditorUtility.SetDirty(activity);
                EditorUtility.SetDirty(activity.sound);
            }
        }

        static BoxCollider IndoorAudioBox(Transform parent, string name, Vector3 min, Vector3 max)
        {
            var area = parent.Find(name);
            if (area == null) area = Group(name, parent, Vector3.zero);
            area.position = (min + max) * 0.5f;
            area.gameObject.layer = 2; // Ignore Raycast
            var box = area.GetComponent<BoxCollider>();
            if (box == null) box = area.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = max - min;
            return box;
        }

        static OutdoorDistraction OutdoorArea(Transform parent, string name, DistractionKind kind, PlayerModeRouter player,
            Vector3 position, Vector3 size, float dwell)
        {
            var area = Group(name, parent, position).gameObject;
            var shape = area.AddComponent<BoxCollider>();
            shape.size = size;
            shape.isTrigger = true;
            var tracker = area.AddComponent<OutdoorDistraction>();
            tracker.kind = kind;
            tracker.player = player;
            tracker.area = shape;
            tracker.dwellSeconds = dwell;
            return tracker;
        }

        static AudioSource OutdoorSound(Transform parent, string name, Vector3 position, AudioClip clip, float volume, float lowPass)
        {
            var emitter = Group(name, parent, position).gameObject;
            var source = emitter.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            emitter.AddComponent<AudioLowPassFilter>().cutoffFrequency = lowPass;
            return source;
        }

        static void MakeNeighbourConversation(Transform parent, PlayerModeRouter player, AudioClip clip)
        {
            var yard = Group("Left neighbour", parent, new Vector3(12.25f, 0, -15.4f));
            Block(yard, "Patio", new Vector3(0, 0.11f, 0), new Vector3(3.1f, 0.22f, 3.5f), Mat("Patio slate", "7C8279"), false);
            OutdoorPerson(yard, "Neighbour on the patio", new Vector3(-0.46f, 0.22f, 0.55f), 145,
                Mat("Neighbour olive", "73884F"), Mat("Warm skin", "CD9779"), OutdoorCharacterMotion.Activity.Talking, 0.8f);
            OutdoorPerson(yard, "Neighbour by the planters", new Vector3(0.56f, 0.22f, -0.48f), -42,
                Mat("Neighbour coral", "B66D61"), Mat("Umber skin", "9B6C53"), OutdoorCharacterMotion.Activity.Talking, 2.0f);
            var activity = OutdoorArea(parent, "Listen at left wall", DistractionKind.Conversation, player,
                new Vector3(7.16f, 1.4f, -15.4f), new Vector3(2.05f, 3.4f, 5.7f), 1.1f);
            activity.sound = OutdoorSound(yard, "Neighbour sound", new Vector3(0, 1.62f, 0), clip, 0.83f, 2350f);
        }

        static void MakeNeighbourParty(Transform parent, PlayerModeRouter player, AudioClip clip)
        {
            var yard = Group("Right neighbour", parent, new Vector3(-13.15f, 0, -15.4f));
            var dark = Mat("Outdoor fixtures", "414647");
            Block(yard, "Party deck", new Vector3(0, 0.115f, 0), new Vector3(4.3f, 0.23f, 4.8f), Mat("Party timber", "816047"), false);
            Block(yard, "Music speaker", new Vector3(-0.6f, 0.68f, -1.75f), new Vector3(0.36f, 0.88f, 0.34f), dark, false);
            foreach (var z in new[] { -2.0f, 2.0f })
                Block(yard, "Light post", new Vector3(1.4f, 1.36f, z), new Vector3(0.10f, 2.72f, 0.10f), dark, false);
            var cable = Group("Light strand", yard, Vector3.zero).gameObject.AddComponent<LineRenderer>();
            cable.useWorldSpace = false;
            cable.sharedMaterial = dark;
            cable.startWidth = cable.endWidth = 0.021f;
            cable.positionCount = 9;
            for (var i = 0; i < cable.positionCount; i++)
            {
                var u = i / (float)(cable.positionCount - 1);
                var position = new Vector3(1.4f, 2.68f - 0.30f * Mathf.Sin(Mathf.PI * u), Mathf.Lerp(-2f, 2f, u));
                cable.SetPosition(i, position);
                var shades = new[] { Mat("Party lantern coral", "EF8375"), Mat("Party lantern teal", "80C8BE"), Mat("Party lantern peach", "FBD393") };
                Primitive(PrimitiveType.Sphere, yard, "Party lantern", position - Vector3.up * 0.10f,
                    new Vector3(0.17f, 0.20f, 0.17f), shades[i % shades.Length], false);
            }
            OutdoorPerson(yard, "Dancer in teal", new Vector3(0.34f, 0.23f, 0.42f), 75,
                Mat("Party teal shirt", "4E9391"), Mat("Umber skin", "9B6C53"), OutdoorCharacterMotion.Activity.Dancing, 0.3f);
            OutdoorPerson(yard, "Dancer in gold", new Vector3(-0.62f, 0.23f, -0.55f), 40,
                Mat("Party gold shirt", "DBA947"), Mat("Warm skin", "CD9779"), OutdoorCharacterMotion.Activity.Dancing, 0.6f);
            var activity = OutdoorArea(parent, "Listen at right wall", DistractionKind.Party, player,
                new Vector3(-7.41f, 1.4f, -15.4f), new Vector3(2.05f, 3.4f, 5.9f), 1.1f);
            activity.sound = OutdoorSound(yard, "Neighbour sound", new Vector3(-0.6f, 0.74f, -1.75f), clip, 0.68f, 1940f);
        }

        static void MakeConstructionSite(Transform parent, PlayerModeRouter player, float z, Collider boundary)
        {
            var site = Group("Construction across the road", parent, new Vector3(0, 0, z));
            var sand = Mat("Construction sand", "C5A681");
            var orange = Mat("Construction orange", "EB8150");
            var cream = Mat("Construction stripe", "FBF0CE");
            var metal = Mat("Outdoor fixtures", "414647");
            Block(site, "Work pad", new Vector3(0, 0.07f, 0), new Vector3(8.9f, 0.14f, 6.6f), sand, false);
            foreach (var x in new[] { -3.15f, 2.98f })
            {
                var fence = Group("Striped work barricade", site, new Vector3(x, 0.14f, 2.7f));
                Block(fence, "Barrier board", new Vector3(0, 0.79f, 0), new Vector3(2.2f, 0.34f, 0.11f), orange, false);
                foreach (var post in new[] { -0.88f, 0.88f })
                    Block(fence, "Barrier leg", new Vector3(post, 0.50f, 0), new Vector3(0.095f, 1f, 0.12f), metal, false);
                for (var i = 0; i < 5; i++)
                    Block(fence, "Reflective strip", new Vector3(-0.86f + i * 0.43f, 0.79f, 0.063f), new Vector3(0.20f, 0.30f, 0.018f), cream, false);
            }
            foreach (var x in new[] { -3.5f, 2.4f, 3.65f })
            {
                Block(site, "Cone base", new Vector3(x, 0.17f, 1.25f), new Vector3(0.49f, 0.06f, 0.49f), metal, false);
                Cylinder(site, "Orange marker", new Vector3(x, 0.45f, 1.25f), new Vector3(0.29f, 0.29f, 0.29f), orange, false);
                Cylinder(site, "Marker band", new Vector3(x, 0.52f, 1.25f), new Vector3(0.30f, 0.047f, 0.30f), cream, false);
            }
            for (var row = 0; row < 3; row++)
                for (var column = 0; column < 4; column++)
                    Block(site, "Stacked block", new Vector3(-3.0f + column * 0.55f, 0.24f + row * 0.20f, -1.74f),
                        new Vector3(0.53f, 0.185f, 0.65f), Mat("Construction masonry", "9A9690"), false);

            foreach (var x in new[] { 1.55f, 3.65f })
                foreach (var rear in new[] { -2.45f, -0.90f })
                    Block(site, "Scaffold upright", new Vector3(x, 1.75f, rear), new Vector3(.10f, 3.25f, .10f), metal, false);
            foreach (var y in new[] { 1.10f, 2.36f })
                Block(site, "Scaffold platform", new Vector3(2.6f, y, -1.68f), new Vector3(2.35f, 0.10f, 1.7f), Mat("Party timber", "816047"), false);

            OutdoorPerson(site, "Walking construction worker", new Vector3(0, 0.14f, 0.1f), 0,
                orange, Mat("Warm skin", "CD9779"), OutdoorCharacterMotion.Activity.Walking, 0f);
            var observer = OutdoorArea(parent, "Watch across the street", DistractionKind.Construction, player,
                new Vector3(0, 1.4f, -23.0f), new Vector3(16.1f, 3.4f, 13.8f), 1.0f);
            observer.gazeTarget = Group("Site gaze point", site, new Vector3(0, 1.45f, 0.43f));
            observer.transparentBoundary = boundary;
        }

        static void OutdoorPerson(Transform parent, string name, Vector3 position, float yaw, Material shirt, Material skin,
            OutdoorCharacterMotion.Activity activity, float phase)
        {
            var root = Group(name, parent, position);
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var motion = root.gameObject.AddComponent<OutdoorCharacterMotion>();
            motion.activity = activity;
            motion.phase = phase;
            var clothes = Mat("Outdoor trousers", "454D61");
            var boot = Mat("Outdoor fixtures", "414647");
            motion.torso = Group("Upper body", root, new Vector3(0, 0.88f, 0));
            Block(motion.torso, "Shirt", new Vector3(0, 0.30f, 0), new Vector3(.46f, .59f, .29f), shirt, false);
            Primitive(PrimitiveType.Sphere, motion.torso, "Head", new Vector3(0, 0.81f, 0), new Vector3(.28f, .34f, .27f), skin, false);
            foreach (var side in new[] { -1, 1 })
            {
                var arm = Group("Arm pivot", motion.torso, new Vector3(side * 0.302f, 0.54f, 0));
                Block(arm, "Sleeve", new Vector3(0, -0.17f, 0), new Vector3(.141f, .36f, .16f), shirt, false);
                Block(arm, "Hand", new Vector3(0, -0.40f, 0), new Vector3(.12f, .14f, .145f), skin, false);
                var leg = Group("Leg pivot", root, new Vector3(side * .126f, 0.88f, 0));
                Block(leg, "Leg", new Vector3(0, -.38f, 0), new Vector3(.191f, .76f, .22f), clothes, false);
                Block(leg, "Shoe", new Vector3(0, -.80f, .06f), new Vector3(.21f, .16f, .36f), boot, false);
                if (side < 0) { motion.leftArm = arm; motion.leftLeg = leg; }
                else { motion.rightArm = arm; motion.rightLeg = leg; }
            }
            if (activity != OutdoorCharacterMotion.Activity.Walking) return;
            var hardHat = Mat("Worker hard hat", "F8C650");
            Primitive(PrimitiveType.Sphere, motion.torso, "Hard hat", new Vector3(0, 0.94f, 0), new Vector3(.35f, .20f, .34f), hardHat, false);
            Block(motion.torso, "Hard hat brim", new Vector3(0, .91f, .052f), new Vector3(.39f, .035f, .38f), hardHat, false);
            Block(motion.torso, "Hi-vis waist band", new Vector3(0, .18f, 0), new Vector3(.465f, .055f, .298f), Mat("Construction stripe", "FBF0CE"), false);
        }
    }
}
