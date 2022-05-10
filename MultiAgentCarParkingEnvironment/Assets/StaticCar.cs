using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StaticCar : MonoBehaviour
{
    public ParkingSpace parkingSpace { get; private  set; }

    public void UpdateParkingSpace(ParkingSpace ps, bool forceExploreConflictingAgents = true)
    {
        if (parkingSpace != null) parkingSpace.InhabitingStaticCar = null;
        parkingSpace = ps;
        ps.InhabitingStaticCar = this;
        if (forceExploreConflictingAgents)
        {
            CarAgent[] psGoalCarAgents = new CarAgent[ps.GoalCarAgents.Count];
            ps.GoalCarAgents.CopyTo(psGoalCarAgents);
            foreach (var psGoalAgent in psGoalCarAgents)
            {
                psGoalAgent.SetExploring();
            }
        }
    }

    public void Delete()
    {
        if (parkingSpace != null) parkingSpace.InhabitingStaticCar = null;
        Destroy(gameObject);
    }
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (parkingSpace != null)
        {
            transform.rotation = parkingSpace.transform.parent.rotation;
            transform.position = new Vector3(parkingSpace.transform.position.x, parkingSpace.transform.position.y, -2);
        }
    }
}
