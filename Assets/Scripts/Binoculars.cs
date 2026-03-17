using System;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class Binoculars : MonoBehaviour
{
    // need header area for adding camera
    // and space for the UI
    [Header("Dependencies")]
    
    // need vcam to use existing followcamera
    public CinemachineVirtualCamera vcam;
    public GameObject binocularUI; // the binocular image will go here

    // Header for Settings
    [Header("Settings")]
    public float zoomFov = 15f;
    public float normalFov = 60f;
    public float zoomSpeed = 5f;

    public bool isUsing = false;

    void Update()
    {
        // Check the toggled state
        // NEW PLAYER INPUT
        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            isUsing = !isUsing;

            if (binocularUI != null)
            {
                binocularUI.SetActive(isUsing);
            }
        }
        
        float target;
        if (isUsing)
        {
            target = zoomFov; // if nocs are on, aim for zoom distance
        }
        else
        {
            target = normalFov; // if they are off, zoom back to normal
        }

        // Using LERP (smooth, gradual zooming) 
        vcam.m_Lens.FieldOfView = Mathf.Lerp(vcam.m_Lens.FieldOfView, target, Time.deltaTime * zoomSpeed);
    }
}
