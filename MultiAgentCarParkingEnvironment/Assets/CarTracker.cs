using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class CarTracker : MonoBehaviour
{
    [SerializeField] public CarAgent CarAgent;
    [SerializeField] public int diameter;
    [SerializeField] public int maxTrackCars;
    [SerializeField] public int maxTrackParkingSpots;
    [SerializeField] public int maxTrackOnlyAgents = -1;
    [SerializeField] public int maxTrackOnlyParkedCars = -1;

    private bool showLines = false;
    private bool dynamicGoals = false;

    private HashSet<ParkingSpace> parkingSpots = new HashSet<ParkingSpace>();
    
    private HashSet<Collider2D> carsAndStaticCars = new HashSet<Collider2D>();
    private Dictionary<int, LineRenderer> objectLines = new Dictionary<int, LineRenderer>();
    private Dictionary<int, GameObject> lineGameObjects = new Dictionary<int, GameObject>();

    public void Init(int _maxTrackCars, int _maxTrackOnlyAgents, int _maxTrackOnlyParkedCars, int _maxTrackParkingSpots, bool _dynamicGoals, bool _showLines)
    {
        showLines = _showLines;
        dynamicGoals = _dynamicGoals;
        maxTrackCars = _maxTrackCars;
        maxTrackOnlyAgents = _maxTrackOnlyAgents;
        maxTrackOnlyParkedCars = _maxTrackOnlyParkedCars;
        maxTrackParkingSpots = _maxTrackParkingSpots;
        carsAndStaticCars = new HashSet<Collider2D>();
        parkingSpots = new HashSet<ParkingSpace>();
        if (!showLines)
        {
            foreach (var lineGo in lineGameObjects.Values)
            {
                Destroy(lineGo);
            }

            objectLines = new Dictionary<int, LineRenderer>();
            lineGameObjects = new Dictionary<int, GameObject>();
        }
    }

    public void OnEnable()
    {
        Init(maxTrackCars, maxTrackOnlyAgents, maxTrackOnlyParkedCars, maxTrackParkingSpots, false, false);
    }

    public List<ParkingSpace> GetNearbyParkingSpots()
    {
        return parkingSpots
            .Where(ps => ps.InhabitingStaticCar == null)
            .OrderBy(c => Vector2.Distance(c.gameObject.transform.position, gameObject.transform.position))
            .Take(maxTrackParkingSpots).ToList();
    }

    public List<Collider2D> GetNearbyCarsAndStaticCars()
    {
        var carsOrderedByDist = carsAndStaticCars.OrderBy(c =>
            Vector2.Distance(c.gameObject.transform.position, gameObject.transform.position)).ToList();
        if (maxTrackOnlyAgents != -1)
        {
            var agents = carsOrderedByDist.Where(c => c.CompareTag("Car"));
            var parkedCars = carsOrderedByDist.Where(c => c.CompareTag("StaticCar"));
            return agents.Take(maxTrackOnlyAgents).Union(parkedCars.Take(maxTrackOnlyParkedCars)).OrderBy(c =>
                Vector2.Distance(c.gameObject.transform.position, gameObject.transform.position)).ToList();
        }
        return carsOrderedByDist.Take(maxTrackCars).ToList();
    }

    public List<Collider2D> GetNearbyCars()
    {
        if (carsAndStaticCars == null) return new List<Collider2D>();
        return carsAndStaticCars.Where(c => c.CompareTag("Car")).OrderBy(c => Vector2.Distance(c.gameObject.transform.position, gameObject.transform.position)).Take(maxTrackOnlyAgents == -1 ? maxTrackCars : maxTrackOnlyAgents).ToList();
    }

    public Collider2D GetNearestCar()
    {
        if (carsAndStaticCars == null) return null;
        var nearbyCars = carsAndStaticCars.Where(c => c.CompareTag("Car")).ToList();
        if (!nearbyCars.Any()) return null;
        Collider2D min = null;
        float minD = float.MaxValue;
        foreach (var nearbyCar in nearbyCars)
        {
            var d = Vector2.Distance(nearbyCar.gameObject.transform.position, gameObject.transform.position);
            if (d < minD)
            {
                minD = d;
                min = nearbyCar;
            }
        }
        return min;
    }
    
    private void OnTriggerEnter2D (Collider2D other)
    {
        if (other.gameObject.GetInstanceID() == gameObject.transform.parent.gameObject.GetInstanceID()) return;

        if (other.CompareTag("ParkingSpace")) parkingSpots.Add(other.GetComponent<ParkingSpace>());

        if (other.CompareTag("Car") || other.CompareTag("StaticCar")) carsAndStaticCars.Add(other); //hashset automatically handles duplicates
        
        // create line
        /*if (!showLines) return;
        GameObject lineGameObject = new GameObject($"line({this.gameObject.GetInstanceID()},{other.gameObject.GetInstanceID()})");
        lineGameObject.transform.position = new Vector3(0, 0, 1);
        LineRenderer lineRenderer = lineGameObject.AddComponent<LineRenderer>();
        lineRenderer.transform.position = new Vector3(0, 0, 1);
        lineRenderer.startWidth = 0.4f;
        objectLines.Add(other.gameObject.GetInstanceID(), lineRenderer);
        lineGameObjects.Add(other.gameObject.GetInstanceID(), lineGameObject);
        lineRenderer.sortingOrder = 1;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.material.color = Color.green;
        lineRenderer.SetPosition(0, gameObject.transform.position);
        lineRenderer.SetPosition(1, other.gameObject.transform.position);*/
    }
  
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("ParkingSpace"))
        {
            ParkingSpace parkingSpace = other.gameObject.GetComponent<ParkingSpace>();
            // get the car that this tracker is attached to, and set it to exploring if its parking spot has gone out of its range
            // TODO: CHECK IF NECESSARY
            /*if (dynamicGoals && CarAgent.parkingSpace == parkingSpace)
            {
                CarAgent.SetExploring();
                Debug.Log("EXPLORING");
            }*/
            parkingSpots.Remove(other.GetComponent<ParkingSpace>());
        }

        if (other.CompareTag("Car") || other.CompareTag("StaticCar"))
        {
            carsAndStaticCars.Remove(other); //hashset automatically handles duplicates
        }
        if (!showLines || !(other.CompareTag("Car") || other.CompareTag("StaticCar") || other.CompareTag("ParkingSpace"))) return;
        /*if (!lineGameObjects.ContainsKey(other.GetInstanceID())) return;
        Destroy(lineGameObjects[other.GetInstanceID()]);
        lineGameObjects.Remove(other.GetInstanceID());
        objectLines.Remove(other.GetInstanceID());*/
    }
    
    // Update is called once per frame
    void Update()
    {
        this.transform.localScale = new Vector3(diameter, diameter, 1);
        if (!showLines) return;

        var newLineGameObjects = new HashSet<GameObject>(
        GetNearbyCarsAndStaticCars().Select(o => o.gameObject).Union(
                GetNearbyParkingSpots().Select(o => o.gameObject)
        ));

        var newLineGameObjectIds = new HashSet<int>(newLineGameObjects.Select(o => o.GetInstanceID()));

        foreach (var lineGo in newLineGameObjects)
        {
            if (!lineGameObjects.ContainsKey(lineGo.GetInstanceID()))
            {
                GameObject lineGameObject = new GameObject($"line({this.gameObject.GetInstanceID()},{lineGo.GetInstanceID()})");
                lineGameObject.transform.position = new Vector3(0, 0, 1);
                LineRenderer lineRenderer = lineGameObject.AddComponent<LineRenderer>();
                lineRenderer.transform.position = new Vector3(0, 0, 1);
                lineRenderer.startWidth = 0.4f;
                objectLines.Add(lineGo.GetInstanceID(), lineRenderer);
                lineGameObjects.Add(lineGo.GetInstanceID(), lineGameObject);
                lineRenderer.sortingOrder = 1;
                lineRenderer.alignment = LineAlignment.TransformZ;
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.material.color = lineGo.CompareTag("ParkingSpace") ? Color.blue : Color.green;
            }
            var lr = objectLines[lineGo.GetInstanceID()];
            lr.SetPosition(0, gameObject.transform.position);
            lr.SetPosition(1, lineGo.transform.position);
        }
        
        foreach (var trackedGoKey in lineGameObjects.Keys.ToList())
        {
            if (newLineGameObjectIds.Contains(trackedGoKey)) continue;
            var lineGo = lineGameObjects[trackedGoKey];
            Destroy(lineGo);
            lineGameObjects.Remove(trackedGoKey);
            objectLines.Remove(trackedGoKey);
        }





        /*HashSet<GameObject> newEnabledLines = new HashSet<GameObject>();
        HashSet<GameObject> newEnabledParkingSpotLines = new HashSet<GameObject>();
        
        
        
        foreach (var collider2d in GetNearbyCarsAndStaticCars())
        {
            var go = lineGameObjects[collider2d.GetInstanceID()];
            var line = objectLines[collider2d.GetInstanceID()];
            go.SetActive(true);
            newEnabledLines.Add(go);
            line.SetPosition(0, gameObject.transform.position);
            line.SetPosition(1, collider2d.gameObject.transform.position);
        }
        foreach (var collider2d in carsAndStaticCars)
        {
            var go = lineGameObjects[collider2d.GetInstanceID()];
            if (!newEnabledLines.Contains(go))
            {
                go.SetActive(false);
            }
        }

        foreach (var parkingSpace in GetNearbyParkingSpots())
        {
            var go = lineGameObjects[parkingSpace.GetInstanceID()];
            var line = objectLines[parkingSpace.GetInstanceID()];
            go.SetActive(true);
            newEnabledParkingSpotLines.Add(go);
            line.SetPosition(0, gameObject.transform.position);
            line.SetPosition(1, parkingSpace.gameObject.transform.position);
        }

        foreach (var parkingSpace in parkingSpots)
        {
            var go = lineGameObjects[parkingSpace.GetInstanceID()];
            if (!newEnabledParkingSpotLines.Contains(go))
            {
                go.SetActive(false);
            }
        }*/
    }
}