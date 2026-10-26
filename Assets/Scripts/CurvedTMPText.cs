using TMPro;
using UnityEngine;

/// <summary>
/// Bends a 3D TextMeshPro's characters around a vertical cylinder centered on this transform —
/// e.g. wrapping a label around a pole so it hugs the surface instead of sitting as a flat plate.
/// Character x-position (left/right along the line) maps to the angle around the cylinder; y stays
/// as laid out. Re-applied every frame since the text content (and therefore each character's
/// position) can change live.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class CurvedTMPText : MonoBehaviour
{
    [Tooltip("Radius of the cylinder the text wraps around, in local units. Match this to how far " +
             "out from the pole's center the text should sit — smaller radius = tighter curve.")]
    public float radius = 0.3f;

    TMP_Text _text;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    void LateUpdate()
    {
        ApplyCurve();
    }

    void ApplyCurve()
    {
        _text.ForceMeshUpdate();
        TMP_TextInfo textInfo = _text.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int vertexIndex = charInfo.vertexIndex;
            int materialIndex = charInfo.materialReferenceIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            // Pivot = the character's own baseline midpoint, used both to compute its angle around
            // the cylinder and as the rotation pivot so each glyph tilts in place (matching the
            // surface) rather than shearing.
            Vector3 pivot = (vertices[vertexIndex + 0] + vertices[vertexIndex + 2]) * 0.5f;

            float angleRad = pivot.x / radius;
            Quaternion rot = Quaternion.AngleAxis(angleRad * Mathf.Rad2Deg, Vector3.up);

            Vector3 curvedPivot = new Vector3(Mathf.Sin(angleRad) * radius, pivot.y, Mathf.Cos(angleRad) * radius);

            for (int v = 0; v < 4; v++)
            {
                Vector3 offsetFromPivot = vertices[vertexIndex + v] - pivot;
                vertices[vertexIndex + v] = curvedPivot + rot * offsetFromPivot;
            }
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            _text.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }
}
