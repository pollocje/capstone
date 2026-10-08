#if UNITY_EDITOR
using UnityEngine;

namespace LuigiGameDev.CarController.Logic
{
    public static class ColliderMeshHelper
    {
        public static GameObject Create(Collider col)
        {
            PrimitiveType? type = GetPrimitiveType(col);
            if (!type.HasValue) return null;

            GameObject go = GameObject.CreatePrimitive(type.Value);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            go.name = col.GetType().Name + "Mesh";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            MatchColliderSize(go.transform, col);

            return go;
        }

        private static PrimitiveType? GetPrimitiveType(Collider col)
        {
            if (col is BoxCollider) return PrimitiveType.Cube;
            if (col is SphereCollider) return PrimitiveType.Sphere;
            if (col is CapsuleCollider) return PrimitiveType.Capsule;

            return null; // Unsupported
        }

        private static void MatchColliderSize(Transform visualTransform, Collider col)
        {
            if (col is BoxCollider box)
            {
                visualTransform.localPosition = box.center;
                visualTransform.localScale = box.size;
            }
            else if (col is SphereCollider sphere)
            {
                visualTransform.localPosition = sphere.center;
                visualTransform.localScale = Vector3.one * sphere.radius * 2f;
            }
            else if (col is CapsuleCollider capsule)
            {
                visualTransform.localPosition = capsule.center;

                Vector3 scale = Vector3.one * capsule.radius * 2f;
                switch (capsule.direction)
                {
                    case 0: scale.x = capsule.height; break;
                    case 1: scale.y = capsule.height; break;
                    case 2: scale.z = capsule.height; break;
                }
                visualTransform.localScale = scale;
            }
        }
    }
}

#endif
