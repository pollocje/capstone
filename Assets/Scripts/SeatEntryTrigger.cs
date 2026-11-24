using UnityEngine;

// Created at runtime by VehicleEnterExit (one per seat, sized/positioned on that seat's own
// Transform) so "press E" enters the seat the player is actually standing next to, instead of
// guessing via nearest-seat distance across one shared trigger spanning the whole vehicle.
public class SeatEntryTrigger : MonoBehaviour
{
    private VehicleEnterExit _vehicle;
    private int _seatIndex;

    public void Init(VehicleEnterExit vehicle, int seatIndex)
    {
        _vehicle = vehicle;
        _seatIndex = seatIndex;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            _vehicle.NotifySeatRange(_seatIndex, other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _vehicle.NotifySeatRange(_seatIndex, other, false);
    }
}
