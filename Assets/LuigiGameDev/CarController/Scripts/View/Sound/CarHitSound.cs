using System.Collections.Generic;
using UnityEngine;

namespace LuigiGameDev.CarController.View
{
    public class CarHitSound : MonoBehaviour
    {
        [SerializeField]
        private AudioSource m_AudioSource;

        [SerializeField]
        private List<GameObject> m_IgnoredObjects = new();

        [Header("Settings")]
        [SerializeField]
        [Range(0.0f, 3.0f)]
        private float m_PitchMin;

        [SerializeField]
        [Range(0.0f, 3.0f)]
        private float m_PitchMax;

        [SerializeField]
        [Min(0.0f)]
        private float m_MaxHitVelocity = 25.0f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float m_BottomHitDotProductThreshold = 0.8f;

        private void OnCollisionEnter(Collision collision)
        {
            if (m_IgnoredObjects.Contains(collision.gameObject))
            {
                return;
            }

            foreach (ContactPoint contact in collision.contacts)
            {
                bool isBottomContact = Vector3.Dot(transform.up, contact.normal) > m_BottomHitDotProductThreshold;

                if (isBottomContact)
                {
                    continue;
                }

                if (!m_AudioSource.isPlaying)
                {
                    float hitVelocityNormalized = collision.relativeVelocity.magnitude / m_MaxHitVelocity;
                    m_AudioSource.volume = hitVelocityNormalized;
                    m_AudioSource.pitch = Mathf.Lerp(m_PitchMin, m_PitchMax, hitVelocityNormalized);
                    m_AudioSource.Play();
                }
            }
        }

        public void AddToIgnored(GameObject go)
        {
            m_IgnoredObjects.Add(go);
        }

        public void RemoveFromIgnored(GameObject go)
        {
            m_IgnoredObjects.Remove(go);
        }
    }
}
