using System.Linq;
using LuigiGameDev.CarController.Logic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Tools > Vehicles > Set Up Steering Wheel
// Adds a pivot at the center of the truck's steering wheel so it can turn,
// adds left/right hand grip points, and turns on IK in PlayerAnimator.
// Safe to run again, it reuses the pivot and grips if they're already there.
public static class SteeringWheelSetup
{
    const string TruckPrefabPath = "Assets/Prefabs/NewPickup.prefab";
    const string ControllerPath = "Assets/Animations/PlayerAnimator.controller";
    const string WheelName = "Pick Up_7 Stearing";
    const string PivotName = "SteeringWheelPivot";
    const float GripAngleAboveCenter = 15f; // hands at about 9:30 and 2:30
    const float GripRimFraction = 0.92f;    // just inside the edge of the wheel

    [MenuItem("Tools/Vehicles/Set Up Steering Wheel")]
    public static void Run()
    {
        if (!EnableIKPass()) return;

        var root = PrefabUtility.LoadPrefabContents(TruckPrefabPath);
        try
        {
            if (!SetUpTruck(root)) return;
            PrefabUtility.SaveAsPrefabAsset(root, TruckPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SteeringWheelSetup] Done: wheel pivot + hand grips added to NewPickup, IK Pass on in PlayerAnimator.");
    }

    static bool EnableIKPass()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[SteeringWheelSetup] {ControllerPath} not found.");
            return false;
        }

        var layers = controller.layers; // this is a copy, so set it back after
        if (!layers[0].iKPass)
        {
            layers[0].iKPass = true;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
        }
        return true;
    }

    static bool SetUpTruck(GameObject root)
    {
        var vehicle = root.GetComponent<VehicleEnterExit>();
        var seat = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat");
        var pivot = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == PivotName);
        var wheel = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == WheelName);
        if (vehicle == null || seat == null || wheel == null)
        {
            Debug.LogError($"[SteeringWheelSetup] NewPickup needs VehicleEnterExit, DriverSeat and '{WheelName}'.");
            return false;
        }

        if (pivot == null)
        {
            var filter = wheel.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                Debug.LogError($"[SteeringWheelSetup] '{WheelName}' has no mesh.");
                return false;
            }

            // Get the wheel vertices relative to the truck
            var parent = wheel.parent;
            Vector3[] pts = mesh.vertices.Select(v => parent.InverseTransformPoint(wheel.TransformPoint(v))).ToArray();
            Vector3 center = Vector3.zero;
            foreach (var p in pts) center += p;
            center /= pts.Length;

            // Wheel is flat, so the axis is the direction it's thinnest in
            Vector3 axis = SmallestVarianceAxis(pts, center);
            Vector3 seatLocal = parent.InverseTransformPoint(seat.position);
            if (Vector3.Dot(axis, seatLocal - center) < 0f) axis = -axis; // face the driver

            // Use the middle of the wheel's edges as the center (spokes throw off the average)
            Vector3 u = Vector3.Cross(axis, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.Cross(axis, Vector3.up).normalized : Vector3.right;
            Vector3 w = Vector3.Cross(u, axis);
            Vector3 c0 = center;
            float uMin = pts.Min(p => Vector3.Dot(p - c0, u)), uMax = pts.Max(p => Vector3.Dot(p - c0, u));
            float wMin = pts.Min(p => Vector3.Dot(p - c0, w)), wMax = pts.Max(p => Vector3.Dot(p - c0, w));
            center += u * ((uMin + uMax) * 0.5f) + w * ((wMin + wMax) * 0.5f);

            float radius = pts.Max(p => Vector3.ProjectOnPlane(p - center, axis).magnitude);

            var pivotGO = new GameObject(PivotName);
            pivot = pivotGO.transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = center;
            pivot.localRotation = Quaternion.LookRotation(axis, Vector3.ProjectOnPlane(Vector3.up, axis));
            wheel.SetParent(pivot, true);

            // Pivot faces the driver, so +X is the driver's left
            float r = radius * GripRimFraction, a = GripAngleAboveCenter * Mathf.Deg2Rad;
            MakeGrip(pivot, "LeftHandGrip", new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), 0f));
            MakeGrip(pivot, "RightHandGrip", new Vector3(-r * Mathf.Cos(a), r * Mathf.Sin(a), 0f));

            Debug.Log($"[SteeringWheelSetup] Wheel center {center}, axis {axis}, radius {radius:F3} (truck space).");
        }

        var left = pivot.Find("LeftHandGrip");
        if (left == null) left = MakeGrip(pivot, "LeftHandGrip", new Vector3(0.15f, 0.04f, 0f));
        var right = pivot.Find("RightHandGrip");
        if (right == null) right = MakeGrip(pivot, "RightHandGrip", new Vector3(-0.15f, 0.04f, 0f));

        var steering = root.GetComponent<SteeringWheel>();
        if (steering == null) steering = root.AddComponent<SteeringWheel>();
        var so = new SerializedObject(steering);
        so.FindProperty("pivot").objectReferenceValue = pivot;
        so.FindProperty("leftHandGrip").objectReferenceValue = left;
        so.FindProperty("rightHandGrip").objectReferenceValue = right;
        so.FindProperty("carInput").objectReferenceValue = root.GetComponent<CarInput>();
        so.ApplyModifiedPropertiesWithoutUndo();

        var vso = new SerializedObject(vehicle);
        vso.FindProperty("steeringWheel").objectReferenceValue = steering;
        vso.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static Transform MakeGrip(Transform pivot, string name, Vector3 localPos)
    {
        var grip = new GameObject(name).transform;
        grip.SetParent(pivot, false);
        grip.localPosition = localPos;
        grip.localRotation = Quaternion.identity;
        return grip;
    }

    // Math to find the direction the points are spread out the least in
    static Vector3 SmallestVarianceAxis(Vector3[] pts, Vector3 c)
    {
        var m = new double[3, 3];
        foreach (var p in pts)
        {
            Vector3 d = p - c;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    m[i, j] += d[i] * d[j];
        }

        var v = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        for (int sweep = 0; sweep < 50; sweep++)
        {
            for (int p = 0; p < 2; p++)
                for (int q = p + 1; q < 3; q++)
                {
                    if (System.Math.Abs(m[p, q]) < 1e-12) continue;
                    double theta = (m[q, q] - m[p, p]) / (2 * m[p, q]);
                    double t = System.Math.Sign(theta) / (System.Math.Abs(theta) + System.Math.Sqrt(theta * theta + 1));
                    if (theta == 0) t = 1;
                    double cs = 1 / System.Math.Sqrt(t * t + 1), sn = t * cs;
                    for (int k = 0; k < 3; k++)
                    {
                        double mkp = m[k, p], mkq = m[k, q];
                        m[k, p] = cs * mkp - sn * mkq;
                        m[k, q] = sn * mkp + cs * mkq;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double mpk = m[p, k], mqk = m[q, k];
                        m[p, k] = cs * mpk - sn * mqk;
                        m[q, k] = sn * mpk + cs * mqk;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = cs * vkp - sn * vkq;
                        v[k, q] = sn * vkp + cs * vkq;
                    }
                }
        }

        int min = 0;
        for (int i = 1; i < 3; i++) if (m[i, i] < m[min, min]) min = i;
        return new Vector3((float)v[0, min], (float)v[1, min], (float)v[2, min]).normalized;
    }
}
