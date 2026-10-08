using System.Collections.Generic;
using LuigiGameDev.CarController.Logic;
using UnityEngine;

namespace LuigiGameDev.CarController.View
{
    /// <summary>
    /// Bridges car logic state to the view layer by feeding data into <see cref="CarViewState"/>.
    /// Supports multiple cars. 
    /// Acts as one of the few View components that directly communicates with Logic,
    /// alongside <see cref="PlayerController"/>.
    /// </summary>
    public class ViewStateLinker : MonoBehaviour
    {
        private class CarEntry
        {
            public GameObject GameObject;
            public Transform Transform;
            public Car Car;
            public Rigidbody Rigidbody;
            public CarInput Input;
            public CarViewState ViewState;
        }

        private List<CarEntry> m_Entries;

        private void Awake()
        {
            m_Entries = new List<CarEntry>();
        }
        
        public void Add(GameObject carGameObject, CarViewState carViewState)
        {
            var car = carGameObject.GetComponent<Car>();
            if (!car) return;

            if (carViewState)
            {
                var settings = carGameObject.GetComponent<CarSettings>();
                if (settings)
                {
                    carViewState.MaxSpeed = settings.MaxSpeed;
                    carViewState.SetAllSpringsMaxLength(settings.SpringMaxLength);
                }
            }

            m_Entries.Add(new CarEntry
            {
                GameObject = carGameObject,
                Transform = carGameObject.transform,
                Car = car,
                Rigidbody = carGameObject.GetComponent<Rigidbody>(),
                Input = carGameObject.GetComponent<CarInput>(),
                ViewState = carViewState
            });
        }

        public void Remove(GameObject carGameObject)
        {
            int index = m_Entries.FindIndex(entry => entry.GameObject == carGameObject);
            if (index >= 0)
            {
                m_Entries.RemoveAt(index);
            }
        }
        
        private void FixedUpdate()
        {
            int count = m_Entries.Count;

            for (int i = 0; i < count; i++)
            {
                CarEntry entry = m_Entries[i];
                if (!entry.GameObject || !entry.Car || !entry.ViewState) continue;

                entry.ViewState.Forward = entry.Transform.forward;
                
                if (entry.Rigidbody)
                {
                    entry.ViewState.LinearVelocity = entry.Rigidbody.linearVelocity;
                }

                if (entry.Input)
                {
                    entry.ViewState.ThrottleInput = entry.Input.Throttle;
                    entry.ViewState.HandbrakeInput = entry.Input.Handbrake;
                }

                for (int wheelId = 0; wheelId < WheelId.Count; wheelId++)
                {
                    entry.ViewState.Springs[wheelId].Length = entry.Car.GetSpringLength(wheelId);
                    entry.ViewState.Springs[wheelId].Velocity = entry.Car.GetSpringVelocity(wheelId);
                    
                    entry.ViewState.Wheels[wheelId].SteerAngle = entry.Car.GetWheelSteerAngle(wheelId);
                    entry.ViewState.Wheels[wheelId].GroundMaterial = entry.Car.GetGroundPhysicsMaterial(wheelId);
                }
            }
        }
    }
}
