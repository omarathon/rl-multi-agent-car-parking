using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Unity.MLAgents;
using Unity.MLAgents.SideChannels;
using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
    [Serializable]
    enum EnvInitMode
    {
        MANUAL,
        SIDE_CHANNEL,
        ENV_PARAMS
    }
    
    public static EnvironmentManager Instance { get; private set; } // singleton
    
    // global objects
    [SerializeField] public Transform drivingArea;
    [SerializeField] public Transform road;
    [SerializeField] public Transform spawnCarArea;

    [SerializeField] public EnvironmentConfig environmentConfig;
    [SerializeField] private EnvInitMode initMode;

    private List<EnvironmentListener> environmentListeners;

    private TrainingSideChannel trainingSideChannel;

    [SerializeField] private CarAgent carAgent;
    [SerializeField] private StaticCar staticCar;

    public List<CarAgent> carAgents;
    public List<ParkingSpace> parkingSpaces;
    public List<ParkingSpace> staticParkingSpaces; // parking spaces inhabited by a staticCar
    public List<StaticCar> staticCars;

    public bool numParkedCarsDeltaPlus;

    public static int NumParkedCarsOscillations = 0;

    private bool ready = false;

    private bool training = true;

    private void OnStartEval()
    {
        SetCarScales(1);
    }
    
    private void OnPreStep()
    {
        if (training && environmentConfig.numStepsTrain != -1 && Academy.Instance.TotalStepCount >= environmentConfig.numStepsTrain)
        {
            training = false;
            OnStartEval();
        }
    }

    public void Awake()
    {
        if (Instance == null) Instance = this;
        //Academy.Instance.OnEnvironmentReset += ResetEnvironment;
        environmentListeners = new List<EnvironmentListener>();
        trainingSideChannel = new TrainingSideChannel(this);
        SideChannelManager.RegisterSideChannel(trainingSideChannel);
        if (initMode == EnvInitMode.MANUAL)
        {
            ONReceieveEnvConfig(environmentConfig);
        }
        else if (initMode == EnvInitMode.ENV_PARAMS)
        {
            // read config
            var ringParams = Academy.Instance.EnvironmentParameters.Keys().Where(k => k.StartsWith("_rd")).ToList();
            
            var paramList = Academy.Instance.EnvironmentParameters.Keys()
                .ToDictionary(k => k, k => Academy.Instance.EnvironmentParameters.GetWithDefault(k, -100));
            var jsonParams = JsonConvert.SerializeObject(paramList);
            Debug.Log($"JSON PARAMS: {jsonParams}");
            var envConfig = JsonConvert.DeserializeObject<EnvironmentConfig>(jsonParams);
            
            // inject ring diameters into environment config
            envConfig.ringDiams = ringParams
                .Select(k => Mathf.RoundToInt(Academy.Instance.EnvironmentParameters.GetWithDefault(k, -100))).ToList();
            
            ONReceieveEnvConfig(envConfig);
        }
    }

    private void InstantiateAgents()
    {
        // Instantiate the agents (1 is a already instantiated)
        var agents = new List<CarAgent>(environmentConfig.numAgents);
        for (int i = 0; i < environmentConfig.numAgents - 1; i++)
        {
            var agent = Instantiate(carAgent);
            Debug.Log("Instantiated agent.");
            RegisterEnvListener(agent);
            agents.Add(agent);
        }
        RegisterEnvListener(carAgent);
        agents.Add(carAgent);

        carAgents = agents;
        Debug.Log("Done instantiating agents.");

        for (int i = 0; i < environmentConfig.numAgents; i++)
        {
            var agentIOtherAgents = new List<CarAgent>(environmentConfig.numAgents - 1);
            for (int j = 0; j < environmentConfig.numAgents; j++)
            {
                if (i == j) continue;
                agentIOtherAgents.Add(agents[j]);
            }
            agents[i].setOtherAgents(agentIOtherAgents);
        }
    }

    private void SetParkingSpaces()
    {
        var parkingSpacesGameObjects = GameObject.FindGameObjectsWithTag("ParkingSpace");
        parkingSpaces = new List<ParkingSpace>(parkingSpacesGameObjects.Select(o => o.GetComponent<ParkingSpace>()));
        // set parking space ids
        int pid = 0;
        foreach (var ps in parkingSpaces)
        {
            ps.id = pid++;
            ps.idnorm = pid / parkingSpaces.Count;
        }
    }

    private void InstantiateStaticCars()
    {
        staticCars = new List<StaticCar>(environmentConfig.numParkedCars);
        for (int i = 0; i < environmentConfig.numParkedCars; i++)
        {
            StaticCar newStaticCar = Instantiate(staticCar);
            staticCars.Add(newStaticCar);
        }
    }
    
    public void RegisterEnvListener(EnvironmentListener listener)
    {
        environmentListeners.Add(listener);
    }

    public void ONReceieveEnvConfig(EnvironmentConfig environmentConfig)
    {
        this.environmentConfig = environmentConfig;
        if (this.environmentConfig.numParkedCarsSecond >= 0)
        {
            numParkedCarsDeltaPlus = this.environmentConfig.numParkedCarsSecond > this.environmentConfig.numParkedCars;
        }
        Academy.Instance.AgentPreStep += i =>
        {
            OnPreStep();
        };
        SetParkingSpaces();
        InstantiateStaticCars();
        InstantiateAgents();
        SetCarScales(environmentConfig.carScaleTrain);
        ResetEnvironment();
        foreach(var envListener in environmentListeners)
        {
            envListener.onReceiveEnv(environmentConfig);
        }
        ready = true;
        Debug.Log("INSTANTIATION SUCCESSFUL, BEGIN");
    }
    
    public void OnDestroy()
    {
        // De-register the side channel
        if (Academy.IsInitialized){
            SideChannelManager.UnregisterSideChannel(trainingSideChannel);
        }
    }

    private void FixedUpdate()
    {
        if (!ready) return;
        if (environmentConfig.numParkedCarsSecond >= 0)
        {
            StatsRecorder sr = Academy.Instance.StatsRecorder;
            sr.Add("Metrics/NumParkedCars Oscillations", NumParkedCarsOscillations, StatAggregationMethod.MostRecent);
            sr.Add("Metrics/NumParkedCars", staticCars.Count, StatAggregationMethod.MostRecent);
        }
    }

    public void ResetEnvironment()
    {
        Debug.Log("RESET ENVIRONMENT");

        // set the agents' goals to random parking spaces if not dynamic goals (need preset goals)
        var agentGoalParkingSpaces = new Dictionary<int, ParkingSpace>(carAgents.Count);
        if (!environmentConfig.dynamicGoals)
        {
            List<int> parkingSpaceIndexes = Helper.GenRandom(0, parkingSpaces.Count - 1, carAgents.Count);
            for (int carIndex = 0; carIndex < carAgents.Count; carIndex++)
            {
                int carId = carAgents[carIndex].GetInstanceID();
                agentGoalParkingSpaces[carId] = parkingSpaces[parkingSpaceIndexes[carIndex]];
                carAgents[carIndex].SetParkingSpace(parkingSpaces[parkingSpaceIndexes[carIndex]]);
            } 
        }
        
        List<ParkingSpace> remainingParkingSpaces = parkingSpaces.Where(p => !agentGoalParkingSpaces.ContainsValue(p)).ToList();
        List<int> staticCarParkingSpaceIndexes =
            Helper.GenRandom(0, remainingParkingSpaces.Count - 1, environmentConfig.numParkedCars);
        
        for (int staticCarIndex = 0; staticCarIndex < staticCars.Count; staticCarIndex++)
        {
            int staticCarParkingSpaceIndex = staticCarParkingSpaceIndexes[staticCarIndex];
            StaticCar curStaticCar = staticCars[staticCarIndex];
            ParkingSpace staticParkingSpace = remainingParkingSpaces[staticCarParkingSpaceIndex];
            // spawn a static car at the transform of the static parking space
            curStaticCar.UpdateParkingSpace(staticParkingSpace);
        }
    }

    public void SpawnParkedCarInSpace(ParkingSpace space)
    {
        StaticCar newStaticCar = Instantiate(staticCar);
        newStaticCar.UpdateParkingSpace(space, environmentConfig.dynamicGoals);
        staticCars.Add(newStaticCar);
    }
    
    public void SpawnParkedCarDuringScene()
    {
        StaticCar newStaticCar = Instantiate(staticCar);
        // where to put? in spot with least number of conflicts, where num conflicts = num agents colliding with parking spot + num agents with parking spot as their goal
        // resolve conflict candidate spots as those furthest away from any agent
        var parkingSpotConflicts =
            parkingSpaces.Select(ps => ps.InhabitingStaticCar != null ? int.MaxValue : ps.GoalCarAgents.Count + ps.InhabitingCarAgents.Count).ToList();
        int minConflicts = parkingSpotConflicts.Min();
        var candidateParkingSpots = new List<ParkingSpace>();
        for (int i = 0; i < parkingSpaces.Count; i++)
        {
            if (parkingSpotConflicts[i] == minConflicts) candidateParkingSpots.Add(parkingSpaces[i]);
        }

        var parkingSpot = candidateParkingSpots[GetObjectFurthestAway(candidateParkingSpots.Select(p => p.gameObject.transform.position).ToList())];
        
        // if any agents are assigned to this spot as their goal or also agents on-top, end their episode early, decrementing episode count as this wasn't a fair episode for the agentsv
        foreach (var conflictingAgent in parkingSpot.GoalCarAgents.Union(parkingSpot.InhabitingCarAgents))
        {
            conflictingAgent.SetExploring();
            conflictingAgent.SetRandomLocation();
            conflictingAgent.EndEpisode();
            CarAgent._numEpisodes--;
            if (conflictingAgent._spawnMethod == CarAgent.SpawnMethod.CRASH) CarAgent._numEpisodesSpawnCrash--;
        }
        newStaticCar.UpdateParkingSpace(parkingSpot);
    }
    
    public void RemoveParkedCarDuringScene()
    {
        var staticCarRemove = GetStaticCarFurthestAway();
        staticCars.Remove(staticCarRemove);
        staticCarRemove.Delete();
    }

    public int GetObjectFurthestAway(List<Vector3> positions)
    {
        int maxIndex = -1;
        float maxd = float.MinValue;
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 pos = positions[i];
            // compute distance to nearest agent
            var mind = carAgents.Min(ca =>
                Vector2.Distance(ca.gameObject.transform.position, pos));
            if (mind > maxd)
            {
                maxd = mind;
                maxIndex = i;
            }
        }
        return maxIndex;
    }

    public StaticCar GetStaticCarFurthestAway()
    {
        int i = GetObjectFurthestAway(staticCars.Select(sc => sc.gameObject.transform.position).ToList());
        return i < 0 ? null : staticCars[i];
    }

    public StaticCar GetStaticCarOutOfRange()
    {
        var staticCarCandidates = staticCars.Where(sc =>
            !carAgents.Any(a =>
                a.nearbyCars.Any(nc => nc.gameObject.GetInstanceID() == sc.GetInstanceID()))).ToList();
        return !staticCarCandidates.Any() ? Helper.RandomElement(staticCars) : Helper.RandomElement(staticCarCandidates);
    }
    
    private IEnumerable<GameObject> GetAllCars()
    {
        return carAgents.Select(c => c.gameObject).Union(staticCars.Select(sc => sc.gameObject)).ToList();
    }
    public void SetCarScales(float scale)
    {
        foreach (var car in GetAllCars())
        {
            car.gameObject.transform.localScale = new Vector3(scale, scale, car.gameObject.transform.localScale.z);
        }
    }
}

public interface EnvironmentListener
{
    public void onReceiveEnv(EnvironmentConfig environmentConfig);
}
