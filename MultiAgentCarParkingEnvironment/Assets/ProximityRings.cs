using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProximityRings : MonoBehaviour
{
    [SerializeField] private ProximityRing proximityRing;

    private List<ProximityRing> rings;

    [SerializeField] private List<int> initialDiams;

    public List<ProximityRing> GetRings()
    {
        return rings;
    }
    
    // Start is called before the first frame update
    void Start()
    {
        //SpawnRings(initialDiams);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SpawnRings(List<int> diameters)
    {
        if (diameters == null) return;
        
        if (rings != null)
        {
            foreach (ProximityRing ring in rings)
            {
                Destroy(ring);
            } 
        }
        
        rings = new List<ProximityRing>(diameters.Count);
        for (int i = 0; i < diameters.Count; i++)
        {
            var ring = Instantiate(proximityRing);
            ring.transform.SetParent(gameObject.transform, worldPositionStays: false);
            proximityRing.diameter = diameters[i];
            rings.Add(ring);
            Debug.Log("Spawned ring");
        }
    }
}
