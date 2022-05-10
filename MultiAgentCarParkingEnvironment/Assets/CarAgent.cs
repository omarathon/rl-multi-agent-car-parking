using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TMPro;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using Random = UnityEngine.Random;

public class CarAgent : Agent, EnvironmentListener
{
    public enum SpawnMethod
    {
        RANDOM,
        CLOSE_GOAL,
        CRASH
    }

    public bool Exploring => parkingSpace == null;
    
    

    public SpawnMethod _spawnMethod;

    public static int _numEpisodes = 0;
    public static int _numEpisodesSpawnCrash = 0;
    
    private static int _numCrashes = 0;
    private static int _numCrashesSpawnCrash = 0;
    private static int _numCrashesStartEpisode = 0;
    private static Dictionary<string, int> _numCrashesTagCount = new Dictionary<string, int> {{"Car", 0}, {"Wall", 0}, {"StaticCar", 0}};

    private static int _numReachedGoal = 0;
    private static int _numReachedGoalSpawnCrash = 0;

    private static int _numHalt = 0;
    private static int _numHaltSpawnCrash = 0;

    private int _numExploreInEps = 0;
    private int _numGoalInEps = 0;

    private int _numMoveTowardsInEps = 0;
    private int _numMoveTowardsExploreInEps = 0;

    static object _updateParkingSpotLock = new object();
    static object _deltaNumParkedCarsLock = new object();
    
    private EnvironmentManager environmentManager => EnvironmentManager.Instance;
    private EnvironmentConfig ec;
    private bool envReady = false;

    [SerializeField] private TMP_Text text;
    [SerializeField] private BehaviorParameters behaviorParameters;
    [SerializeField] private Transform proximityRadius;
    [SerializeField] private CarTracker carTracker;

    private Transform drivingArea => environmentManager.drivingArea;
    private Transform road => environmentManager.road;
    public ParkingSpace parkingSpace;
    private Vector2 parkingSpaceModelPos => WorldPosToModelPos(new Vector2(parkingSpace.transform.position. x, parkingSpace.transform.position.y));

    /*[SerializeField] private Vector2 originalPos;
    [SerializeField] private Vector2 originalPosRandomness;*/

    private int pMaxMdp => (int)((drivingArea.localScale.x / mdpToWorldPosScaler) / 2);

    [SerializeField] private int velocity;

    //private Vector2 position;
    [SerializeField] private int rotation;

    private Vector2 position;
    private float mdpToWorldPosScaler;

   // private int maxManhDistance;

    private List<CarAgent> otherAgents;

    public int agentIndex;

    private float maxD;
    private float maxXYD;
    private ProximityRings rings;

    private Queue<List<int>> ringCountHistory;

    private Vector2 nearestCarPosBeforeAction = Vector2.zero;
    private bool gotNearestCarPosBeforeAction = false;

    public bool successfulParkResetting = false;
    
    public void setOtherAgents(List<CarAgent> otherAgents)
    {
        this.otherAgents = otherAgents;
        environmentManager.RegisterEnvListener(this);
    }

    private StatsRecorder sr => Academy.Instance.StatsRecorder;
    
    private float maxTrackDist => (ec.obsNearbyCarsDiameter / 2f + 4.472f) / mdpToWorldPosScaler;

    public List<ParkingSpace> nearbyParkingSpots = new List<ParkingSpace>();
    public List<Collider2D> nearbyCars = new List<Collider2D>();

    public void onReceiveEnv(EnvironmentConfig environmentConfig)
    {
        Debug.Log(gameObject.name);
        Debug.Log("Received env config " + JsonConvert.SerializeObject(environmentConfig));
        ec = environmentConfig;
        
        InitRingObsHistory();
        
        // init tracker
        if (ec.obsNearbyCars)
        {
            carTracker.diameter = environmentConfig.obsNearbyCarsDiameter;
            carTracker.Init(environmentConfig.obsNearbyCarsCount,
                environmentConfig.obsNearbyCarsAgentCount,
                environmentConfig.obsNearbyCarsParkedCount,
                environmentConfig.obsNearbyParkingSpotsCount,
                environmentConfig.dynamicGoals,
                environmentConfig.visualiseNearbyCars);
        }
        else
        {
            carTracker.enabled = false;
            carTracker.gameObject.SetActive(false);
        }
        
        rings = GetComponent<ProximityRings>();
        rings.SpawnRings(environmentConfig.ringDiams);

        // setup environment
        
        var behParams = GetComponent<BehaviorParameters>();

        var numObs = ec.ComputeNumObs();
        behParams.BrainParameters.VectorObservationSize = numObs;
        behParams.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(ec.ComputeDiscreteActionRanges());

        MaxStep = ec.maxSteps;

        Debug.Log(
            $"Num obs: {numObs}\nDiscrete obs ranges: {string.Join(",", ec.ComputeDiscreteObsRanges())}\nDiscrete action ranges: {string.Join(",", ec.ComputeDiscreteActionRanges())}");

        // == CONSTANTS ==
        maxD = Mathf.Sqrt(2 * Mathf.Pow(drivingArea.localScale.x / ec.posSize, 2));
        maxXYD = drivingArea.localScale.x / ec.posSize;
        agentIndex = environmentManager.carAgents.IndexOf(this);
        
        InitEpisode();
        
        behParams.enabled = true;
        gameObject.SetActive(true);
        envReady = true;
        
        // End episode for all agents to make the environment reset
        EndEpisode();

        Debug.Log("BEGIN");
    }

    private void InitRingObsHistory()
    {
        ringCountHistory = new Queue<List<int>>();
        for (int i = 0; i < ec.ringNumPrevObs; i++)
        {
            // 0 in each ring
            List<int> ringCounts = new List<int>();
            for (int r = 0; r < ec.ringDiams.Count; r++)
            {
                ringCounts.Add(0);
            }
            ringCountHistory.Enqueue(ringCounts);
        }
    }

    public void SetExploring()
    {
        if (Exploring) return;
        parkingSpace.RemoveGoalAgent(this);
        parkingSpace = null;
    }

    public void SetParkingSpace(ParkingSpace newParkingSpace)
    {
        if (!Exploring) parkingSpace.RemoveGoalAgent(this);
        newParkingSpace.AddGoalAgent(this);
        parkingSpace = newParkingSpace;
    }

    private void InitEpisode()
    {
        // mdp has only int values whereas env may have decimals
        mdpToWorldPosScaler =  ec.posSize;
        rotation = Random.Range(0, 360 / ec.thetaSize - 1) *  ec.thetaSize; // random initial rotation
        gameObject.transform.eulerAngles = new Vector3(0, 0, -rotation);
        velocity = 0;

        _numExploreInEps = 0;
        _numGoalInEps = 0;
        _numMoveTowardsInEps = 0;
        _numMoveTowardsExploreInEps = 0;
        
        if (ec.obsRings)
        {
            InitRingObsHistory();
        }

        if (ec.dynamicGoals)
        {
            SetExploring();            
        }
        else
        {
            UpdateParkingSpot();
        }
        SetRandomLocation(true);

        nearbyParkingSpots = carTracker.GetNearbyParkingSpots();

        sr.Add("Metrics/Num Episodes", ++_numEpisodes, StatAggregationMethod.MostRecent);
    }

    private void OnCollision(Collider2D other)
    {
        var otherTag = other.tag;
        if (!otherTag.Equals("Wall") && !otherTag.Equals("Car") && !otherTag.Equals("StaticCar")) return;
        // want to know # of crashes, # of times didn't crash but not reach goal (halt) and # of times reaches goal
        sr.Add("Metrics/Total Crashes", ++_numCrashes, StatAggregationMethod.MostRecent);

        _numCrashesTagCount[otherTag] = _numCrashesTagCount[otherTag] + 1;
        sr.Add($"Metrics/Total Crashes {otherTag}", _numCrashesTagCount[otherTag], StatAggregationMethod.MostRecent);

        if (_spawnMethod == SpawnMethod.CRASH)
        {
            sr.Add("Metrics/Total Crashes SpawnCrash", ++_numCrashesSpawnCrash, StatAggregationMethod.MostRecent);
        }

        if (StepCount < 2)
        {
            sr.Add("Metrics/Total Crashes StartEpisode", ++_numCrashesStartEpisode, StatAggregationMethod.MostRecent);
        }

        float velocityFraction = Mathf.Abs(velocity) / ((float)Mathf.Max(ec.maxVelocityMagnitude,
            ec.minVelocityMagnitude));
        AddReward(-((ec.rewCrash - ec.rewCrashVelocitySum) + ec.rewCrashVelocitySum * velocityFraction));

        OnEpisodeEnd();
        //Debug.Log($"CRASHES: {_numCrashes}");
        EndEpisode();
    }
    
    public void OnSuccessfulPark()
    {
        //Debug.Log("PARKED");
        successfulParkResetting = true;
        
        int goalDeltaPose = ComputeGoalDeltaPose(parkingSpace, true);
        float posePenalty = ec.rewFinalPoseSum * (goalDeltaPose / 180f);
        float velocityPenalty = ec.rewFinalVelocitySum * (Mathf.Abs(velocity / ec.velocityGranularity) /
                                                          (float) Mathf.Max(ec.minVelocityMagnitude,
                                                              ec.maxVelocityMagnitude));
        AddReward(ec.rewReachGoal - posePenalty - velocityPenalty);
        sr.Add("Metrics/Total Reached Goal", ++_numReachedGoal, StatAggregationMethod.MostRecent);
        sr.Add("Metrics/Park Velocity", Mathf.Abs(velocity), StatAggregationMethod.Average);
        if (_spawnMethod == SpawnMethod.CRASH)
        {
            sr.Add("Metrics/Total Reached Goal SpawnCrash", ++_numReachedGoalSpawnCrash, StatAggregationMethod.MostRecent);
        }

        ParkingSpace parkedParkingSpace = parkingSpace;

        bool moveStaticCarIntoSpot = true;
        
        if (ec.numParkedCarsSecond >= 0)
        {
            // we oscillate num parked cars
            lock (_deltaNumParkedCarsLock)
            {
                if (environmentManager.numParkedCarsDeltaPlus)
                {
                    // spawn car in the parking spot so +1 parked cars
                    environmentManager.SpawnParkedCarInSpace(parkedParkingSpace);
                    moveStaticCarIntoSpot = false;
                    // update oscillation parked cars direction
                    if (environmentManager.staticCars.Count >= Math.Max(ec.numParkedCars,
                        ec.numParkedCarsSecond))
                    {
                        environmentManager.numParkedCarsDeltaPlus = false;
                        EnvironmentManager.NumParkedCarsOscillations++;
                    }
                }
                else
                {
                    var scRemove = environmentManager.GetStaticCarOutOfRange();
                    environmentManager.RemoveParkedCarDuringScene();
                    environmentManager.staticCars.Remove(scRemove);
                    scRemove.Delete();
                    // update oscillation parked cars direction
                    if (environmentManager.staticCars.Count <= Math.Min(ec.numParkedCars, ec.numParkedCarsSecond))
                    {
                        environmentManager.numParkedCarsDeltaPlus = true;
                        EnvironmentManager.NumParkedCarsOscillations++;
                    }
                }
            }
        }
        if (moveStaticCarIntoSpot && environmentManager.staticCars.Count > 0)
        {
            // static car to move into spot must be out of sensing range of other agents
            // note if num static cars = 20, num agents = 6, num nearby cars sense = 3, this is safe because 6*3 = 18 cars may be sensed, leaving 2 safe to remove unnoticed
            var scReplace = environmentManager.GetStaticCarOutOfRange();
            scReplace.UpdateParkingSpace(parkingSpace, ec.dynamicGoals);
        }
        OnEpisodeEnd();
        EndEpisode();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!envReady || successfulParkResetting) return;
        OnCollision(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!envReady || successfulParkResetting) return;
        OnCollision(other);
    }

    private Vector2 ComputeNewPosition()
    {
        Vector2 newPosition = new Vector2();
        float rotationRadians = Mathf.Deg2Rad * rotation;
        newPosition.x = Mathf.RoundToInt(position.x + velocity * Mathf.Sin(rotationRadians));
        newPosition.y = Mathf.RoundToInt(position.y + velocity * Mathf.Cos(rotationRadians));
        return newPosition;
    }

    private void FixedUpdate()
    {
        if (!envReady) return;
        text.text = $"{agentIndex.ToString()}{(Exploring ? "e" : "")}";
        
        gameObject.transform.eulerAngles = new Vector3(0, 0, -rotation);
        Vector2 newPosition = ComputeNewPosition();

        if (Exploring) _numExploreInEps++;
        else _numGoalInEps++;

        bool moveTowardsGoal = !Exploring && MoveTowardsParkingSpot(position, newPosition, parkingSpace);
        bool moveTowardsAnySensedGoal = ec.dynamicGoals && Exploring && nearbyParkingSpots.Any(p => MoveTowardsParkingSpot(position, newPosition, p));

        if (moveTowardsGoal) _numMoveTowardsInEps++;
        if (moveTowardsAnySensedGoal) _numMoveTowardsExploreInEps++;
        
        // check if x or y became out of bounds, in which case we reset the episode
        if ((Mathf.Abs(newPosition.x) > pMaxMdp) || (Mathf.Abs(newPosition.y) > pMaxMdp))
        {
            AddReward(-ec.rewCrash);
            OnEpisodeEnd();
            EndEpisode();
            return;
        }
        
        // reward based on moving towards goal or not
        if (ec.rewDistSum > 0 && !Exploring)
        {
            if (!moveTowardsGoal) // essentially equal or distance has increased to the goal - punish since not moving towards goal
            {
                AddReward(-ec.rewDistSum / MaxStep);
            }
            else // distance has decreased to the goal, so moving towards it, reward
            {
                AddReward(ec.rewDistSum / MaxStep);
            }
        }
        
        // reward based on delta pose to goal (dense)
        if (!Exploring)
        {
            int goalDeltaPose = ComputeGoalDeltaPose(parkingSpace, true);
            AddReward((ec.rewDenseDeltaGoalPoseSum / MaxStep) * -goalDeltaPose / 180f);
        }

        // reward based on distance to nearby active cars
        if (ec.rewNearbyCarsDistNumCars > 0)
        {
            // sum dist of `rewNearbyCarsDistNumCars` nearest cars, padding rest with max senseable dist if not enough
            float sumDist = 0;
            var nearbyCars = carTracker.GetNearbyCars().Take(ec.rewNearbyCarsDistNumCars).ToList();
            foreach (var nearbyCar in nearbyCars)
            {
                CarAgent nearbyCarAgent = nearbyCar.gameObject.GetComponent<CarAgent>();
                sumDist += Vector2.Distance(position, nearbyCarAgent.position);
            }
            // pad rest
            for (int i = 0; i < ec.rewNearbyCarsDistNumCars - nearbyCars.Count; i++)
            {
                sumDist += maxTrackDist;
            }
            // regularise
            float sumDistReg = sumDist / (ec.rewNearbyCarsDistNumCars * maxTrackDist);
            AddReward(-(ec.rewNearbyCarsDistSum / MaxStep) * sumDistReg);
        }

        UpdatePosition(newPosition);
        
        // constant time punishment
        AddReward(-ec.rewTimeSum / MaxStep);
        
        if (!Exploring && Math.Abs(gameObject.transform.position.x - parkingSpace.transform.position.x) < 1.001f && Math.Abs(gameObject.transform.position.y - parkingSpace.transform.position.y) < 1.001f)
        {
            // In Parking Space, thus reward and end episode
            OnSuccessfulPark();
        }

        // detect halt
        if (StepCount >= MaxStep - 1 && MaxStep >= 0)
        {
            sr.Add("Metrics/Total Halted", ++_numHalt, StatAggregationMethod.MostRecent);
            if (_spawnMethod == SpawnMethod.CRASH) sr.Add("Metrics/Total Halted SpawnCrash", ++_numHaltSpawnCrash, StatAggregationMethod.MostRecent);

            if (ec.rewHalt)
            {
                AddReward(-0.2f);
            }
            OnEpisodeEnd();
        }
    }

    private bool MoveTowardsParkingSpot(Vector2 originalPos, Vector2 newPos, ParkingSpace targetSpot)
    {
        Vector2 targetSpotModelPos = WorldPosToModelPos(targetSpot.gameObject.transform.position);
        float dPrev = Vector2.Distance(targetSpotModelPos, originalPos);
        float dNew = Vector2.Distance(targetSpotModelPos, newPos);
        return Math.Abs(dNew - dPrev) >= 0.00001 && dNew < dPrev;
    }

    private void OnEpisodeEnd()
    {
        if (StepCount == 0) return;
        if (ec.dynamicGoals)
        {
            sr.Add("Metrics/RatioExplorePerEps", _numExploreInEps / (float)StepCount, StatAggregationMethod.Average);
            sr.Add("Metrics/RatioGoalPerEps", _numGoalInEps / (float)StepCount, StatAggregationMethod.Average);
            if (_numGoalInEps > 0) sr.Add("Metrics/RatioMoveTowardsPerEps", _numMoveTowardsInEps / (float)_numGoalInEps, StatAggregationMethod.Average);
            if (_numExploreInEps > 0) sr.Add("Metrics/RatioMoveTowardsExploringPerEps", _numMoveTowardsExploreInEps / (float)_numExploreInEps, StatAggregationMethod.Average);
        }
        else
        {
            // non dynamic goals thus always have goal, never exploring
            sr.Add("Metrics/RatioMoveTowardsPerEps", _numMoveTowardsInEps / (float)StepCount, StatAggregationMethod.Average);
        }
    }

    public override void OnEpisodeBegin()
    {
        if (!envReady) return;
        base.OnEpisodeBegin();
        InitEpisode();
        successfulParkResetting = false;
    }

    public float ComputeLocalAngleToGoal(ParkingSpace ps)
    {
        var modelPos = WorldPosToModelPos(ps.gameObject.transform.position);
        var angleToGoal = AngleTo(position, modelPos);
        return ec.normaliseObs ? angleToGoal / (360f / ec.thetaSize) : angleToGoal;
    }

    public float ComputeDistanceToGoal(ParkingSpace ps)
    {
        var modelPos = WorldPosToModelPos(ps.gameObject.transform.position);
        float d = Vector2.Distance(modelPos, position);
        return d / maxD;  
    }

    public float ComputeVelocity()
    {
        int v = velocity / ec.velocityGranularity + ec.minVelocityMagnitude;
        return ec.normaliseObs ? v / (ec.minVelocityMagnitude + ec.maxVelocityMagnitude + 1f) : v;
    }

    public int ComputeGoalDeltaPose(ParkingSpace ps, bool half)
    {
        int r = Helper.Mod((int) -ps.transform.parent.rotation.eulerAngles.z, 360);
        int dr = Helper.Mod(r - rotation, 360);
        if (!half) return dr;
        int drCap = Helper.Mod(2 * dr, 360);
        return 180 - Math.Abs(180 - drCap);
    }

    public void AddObservationsNearbyParkingSpot(VectorSensor sensor, ParkingSpace ps)
    {
        if (ps == null)
        {
            for (int i = 0; i < ec.ComputeNumObsParkingSpace(); i++)
            {
                sensor.AddObservation(-1f);
            }
            return;
        }
        
        if (ec.obsGoalDeltaPose)
        {
            var parkingSpacePoseLocalized = ComputeGoalDeltaPose(ps, false);
            var parkingSpacePoseLocalizedNorm = parkingSpacePoseLocalized / 360f;
            sensor.AddObservation(parkingSpacePoseLocalizedNorm);
            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: goalActualAngle={parkingSpacePoseLocalizedNorm}");
        }

        if (ec.obsDist)
        {
            float d = ComputeDistanceToGoal(ps);
            sensor.AddObservation(d);
            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: d={d}");
        }

        if (ec.obsXY)
        {
            var psModelPos = WorldPosToModelPos(ps.gameObject.transform.position);
            Vector2 d = (psModelPos - position) / maxXYD;
            sensor.AddObservation(d.x);
            sensor.AddObservation(d.y);
            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: xyDist=({d.x}, {d.y})");
        }
        
        if (ec.obsAngle)
        {
            float angleToGoal = ComputeLocalAngleToGoal(ps);
            sensor.AddObservation(angleToGoal);
            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: angle={angleToGoal}");
        }

        if (ec.obsParkingSpotClosestAgent)
        {
            float d = environmentManager.carAgents.Min(c =>
                Vector2.Distance(c.gameObject.transform.position, ps.gameObject.transform.position)) / maxD;
            sensor.AddObservation(d);
            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: parkingSpotClosestAgentDist={d}");
        }

        if (ec.obsParkingSpotClosestGoalAgent)
        {
            float d = ps.GoalCarAgents.Count == 0 ? -1f : ps.GoalCarAgents.Min(c =>
                Vector2.Distance(c.gameObject.transform.position, ps.gameObject.transform.position)) / maxD;
            sensor.AddObservation(d);
            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: parkingSpotClosestGoalAgentDist={d}");
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!envReady)
        {
            return;
        }

        base.CollectObservations(sensor);

        nearbyParkingSpots = carTracker.GetNearbyParkingSpots();
        nearbyCars = carTracker.GetNearbyCarsAndStaticCars();

        // nearby car dist metric
        if (nearbyCars.Any())
        {
            var nearestCar = nearbyCars[0];
            sr.Add("Metrics/NearestCarDistAvg", Vector2.Distance(nearestCar.gameObject.transform.position, gameObject.transform.position));
        }

        if (ec.obsThetaCorrector)
        {
            float thetaCorrect = 45 % ec.thetaSize == 0 ? (rotation % 45) / (float)45 : (rotation % 90) / (float)90;
            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: thetaCorrector={thetaCorrect}");
            sensor.AddObservation(thetaCorrect);
        }

        if (ec.obsTimestep)
        {
            float t = StepCount / ((float)MaxStep);
            sensor.AddObservation(t);
        }

        // velocity map to 0..n-1 by adding min magnitude and div velocitygranularity
        float v = ComputeVelocity();
        sensor.AddObservation(v);
        if (ec.debugObs) Debug.Log($"OBS {agentIndex}: vel={v}");

        if (ec.obsGlobalAngle)
        {
            var globalThetaNorm = (360-rotation) / 360f;
            sensor.AddObservation(globalThetaNorm);
            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: globalTheta={globalThetaNorm}");
        }

        var parkingSpotsObs = ec.dynamicGoals
            ? nearbyParkingSpots
            : new List<ParkingSpace>(new ParkingSpace[] {parkingSpace});
        

        foreach (var ps in parkingSpotsObs)
        {
            AddObservationsNearbyParkingSpot(sensor, ps);
        }

        if (ec.dynamicGoals)
        {
            // ensure consistent number of parking space obs
            for (int i = 0; i < ec.obsNearbyParkingSpotsCount - parkingSpotsObs.Count; i++)
            {
                AddObservationsNearbyParkingSpot(sensor, null);
            }
        }

        if (ec.obsRings)
        {
            List<ProximityRing> ringList = rings.GetRings();
            List<int> ringCounts = new List<int>();
            foreach (var ring in ringList)
            {
                var colliders = ring.GetColliders();
                if (ec.ringOnlyWall)
                {
                    int numWalls = colliders.Count(c => c.CompareTag("Wall"));
                    ringCounts.Add(numWalls);
                    continue;
                }
                int numCars = colliders.Count(c => c.CompareTag("Car")) - 1; // except self
                int numStaticObject = colliders.Count(c => c.CompareTag("Wall") || c.CompareTag("StaticCar"));
                int numObjectsInRing = Math.Min(numCars + numStaticObject, ec.ringMaxNumObjTrack); // cap by max number of objects to track in each ring
                ringCounts.Add(numObjectsInRing);
            }
            // add observation of current ring as well as old rings
            int rx = 0;
            foreach (var rc in ringCounts)
            {
                sensor.AddObservation(ec.normaliseObs ? rc / (ec.ringMaxNumObjTrack + 1f) : rc);
                if (ec.debugObs) Debug.Log($"OBS {agentIndex}: ringH0I{rx++}={(ec.normaliseObs ? rc / (ec.ringMaxNumObjTrack + 1f) : rc)}");
            }
            int rh = 1;
            foreach (var oldRingCounts in ringCountHistory)
            {
                rx = 0;
                foreach (var rc in oldRingCounts)
                {
                    sensor.AddObservation(ec.normaliseObs ? rc / (ec.ringMaxNumObjTrack + 1f) : rc);
                    if (ec.debugObs) Debug.Log($"OBS {agentIndex}: ringH{rh++}I{rx++}={(ec.normaliseObs ? rc / (ec.ringMaxNumObjTrack + 1f) : rc)}");
                }
            }
            // dequeue oldest ring count from history and add new
            if (ec.ringNumPrevObs > 0)
            {
                ringCountHistory.Dequeue();
                ringCountHistory.Enqueue(ringCounts);
            }
        }

        if (ec.obsNearbyCars)
        {
            foreach (var nearbyCar in nearbyCars)
            {
                Transform t = nearbyCar.gameObject.transform;
                CarAgent otherCar = nearbyCar.CompareTag("StaticCar") ? null : nearbyCar.gameObject.GetComponent<CarAgent>();
                
                // convert its position to model coordinates
                Vector2 p = WorldPosToModelPos(t.position);
                float d = Vector2.Distance(position, p) / maxD;
                sensor.AddObservation(d);
                
                // delta theta
                int theta = AngleTo(position, p);
                float thetaNorm = NormalizeMultipleAngle(theta);
                sensor.AddObservation(thetaNorm);

                // delta orientation
                int r = otherCar == null ? Helper.Mod((int) -t.rotation.eulerAngles.z, 360) : otherCar.rotation;
                int dr = Helper.Mod(r - rotation, 360);
                int drCap = ec.obsNearbyCarsPoseHalf ? Helper.Mod(2 * dr, 360): dr;
                float drCapNorm = drCap / 360f;
                sensor.AddObservation(drCapNorm);

                if (ec.debugObs) Debug.Log($"OBS {agentIndex}: nearbyCar=(d={d}, unregD={d * maxD}, theta={thetaNorm}, poseThetaCap={drCapNorm})");

                if (ec.obsNearbyCarsParked)
                {
                    bool parked = nearbyCar.gameObject.CompareTag("StaticCar");
                    sensor.AddObservation(parked ? 1f : 0f);
                    if (ec.debugObs) Debug.Log($"OBS {agentIndex}: nearbyCarParked={parked}");
                }

                if (ec.obsNearbyCarsGoal)
                {
                    if (nearbyCar.gameObject.CompareTag("StaticCar"))
                    {
                        if (ec.obsGoalOneHot)
                        {
                            AddVoidGoalObservation(sensor);
                        }
                        else
                        {
                            sensor.AddObservation(-1f); // has no angle to goal
                            if (!ec.dynamicGoals) sensor.AddObservation(-1f); // has no distance to goal
                        }
                        if (ec.debugObs) Debug.Log("OBS {agentIndex} nearbyCarGoal=void");
                    }
                    else
                    {
                        // NOTE: Here we use index in own parking space observation list.
                        if (ec.dynamicGoals)
                        {
                            float nearbyCarGoalObs = otherCar.AddGoalIndexObservation(sensor, nearbyParkingSpots);
                            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: nearbyCarGoal=({nearbyCarGoalObs})");
                        }
                        else
                        {
                            var otherCarAngleToGoal = otherCar.ComputeLocalAngleToGoal(otherCar.parkingSpace);
                            var otherCarDistToGoal = otherCar.ComputeDistanceToGoal(otherCar.parkingSpace);
                            sensor.AddObservation(otherCarAngleToGoal);
                            sensor.AddObservation(otherCarDistToGoal);
                            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: nearbyCarGoal=(angle={otherCarAngleToGoal}, d={otherCarDistToGoal})");
                        }
                    }
                }

                if (ec.obsNearbyCarsVelocity)
                {
                    float vOther = nearbyCar.gameObject.CompareTag("StaticCar")
                        ? 0
                        : otherCar.ComputeVelocity();
                    sensor.AddObservation(vOther);
                    if (ec.debugObs) Debug.Log($"OBS {agentIndex}: nearbyCarVel={vOther}");
                }
            }
            // pad rest with nothing
            for (int i = 0; i < ec.NumCarsTrack - nearbyCars.Count; i++)
            {
                sensor.AddObservation(-1);
                sensor.AddObservation(-1);
                sensor.AddObservation(-1);
                if (ec.obsNearbyCarsParked) sensor.AddObservation(-1);
                if (ec.obsNearbyCarsGoal)
                {
                    if (ec.dynamicGoals && ec.obsGoalOneHot)
                    {
                        AddVoidGoalObservation(sensor);
                    }
                    else
                    {
                        sensor.AddObservation(-1);
                        if (!ec.dynamicGoals) sensor.AddObservation(-1);
                    }
                }
                if (ec.obsNearbyCarsVelocity) sensor.AddObservation(-1);
                if (ec.debugObs) Debug.Log($"OBS {agentIndex}: nearbyCar=void(-1)");
            }
        }

        // add current goal parking space id as obs
        if (ec.dynamicGoals)
        {
            // exploring val = 0, parking spot id = index of parkingspot in nearby parking spots
            if (!successfulParkResetting && !Exploring && !nearbyParkingSpots.Contains(parkingSpace))
            {
                sr.Add("Metrics/Num Lost Goal", ++_numLostGoal, StatAggregationMethod.MostRecent);
                UpdateDeltaGoal(parkingSpace, null, false);
                SetExploring();
            }

            float parkingSpotObs = AddGoalIndexObservation(sensor, nearbyParkingSpots);
            if (ec.debugObs) Debug.Log($"OBS {agentIndex}: parkingSpot={parkingSpotObs}");
        }
    }

    private void AddVoidGoalObservation(VectorSensor sensor)
    {
        for (int i = 0; i < ec.obsNearbyParkingSpotsCount + 1; i++)
        {
            sensor.AddObservation(0.0f);
        }

    }
    
    public float AddGoalIndexObservation(VectorSensor sensor, List<ParkingSpace> senseParkingSpots)
    {
        if (ec.obsGoalOneHot)
        {
            int oneHotObs = Exploring || !senseParkingSpots.Contains(parkingSpace)
                ? 0
                : senseParkingSpots.IndexOf(parkingSpace) + 1;
            sensor.AddOneHotObservation(oneHotObs, ec.obsNearbyParkingSpotsCount + 1);
            return oneHotObs;
        }
        float obs = Exploring || !senseParkingSpots.Contains(parkingSpace) ? -1 : senseParkingSpots.IndexOf(parkingSpace) / (float)ec.obsNearbyParkingSpotsCount;
        sensor.AddObservation(obs);
        return obs;
    }

    private float NormalizeMultipleAngle(int angle)
    {
        return angle / (360f / ec.thetaSize);
    }
    
    // returns a multiple angle
    private int AngleTo(Vector2 modelFrom, Vector2 modelTo)
    {
        var delta = modelTo - modelFrom;
        // get angle to goal
        var theta = Granulate(-Vector2.SignedAngle(new Vector2(0, 1), delta), ec.thetaSize);
        // theta is between -180 and 180 =
        var dtheta = Helper.Mod(Helper.Mod(theta, 360) - rotation, 360) / ec.thetaSize;
        return dtheta;
    }
    

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (!envReady) return;
        base.OnActionReceived(actionBuffers);
        int deltaVRaw =
            actionBuffers
                .DiscreteActions[
                    0]; // in multiples of velocityGranularity, from 0 to minDeltaVMagnitude + maxDeltaVMagnitude, centered at minDeltaVMagnitude 
        int deltaThetaRaw =
            actionBuffers
                .DiscreteActions[
                    1]; // in multiples of thetaGranularity, from 0 to maxDeltaThetaMagnitude * 2, centered at maxDeltaThetaMagnitude

        int deltaV = deltaVRaw - ec.minDeltaVMagnitude;
        int deltaTheta = deltaThetaRaw - ec.maxDeltaThetaMagnitude;

        var oldWorldPos = gameObject.transform.position;

        velocity = Mathf.Clamp(velocity + deltaV * ec.velocityGranularity,
            -ec.minVelocityMagnitude * ec.velocityGranularity, ec.maxVelocityMagnitude * ec.velocityGranularity);

        // do not allow steering while halted
        if (velocity == 0) deltaTheta = 0;

        rotation = Helper.Mod((rotation + deltaTheta * ec.thetaSize), 360);
        
        // punish proportional to size of delta theta
        var magVelNormDThetaPunishment = ec.rewDeltaThetaVelMult
            ? (float) Math.Abs(velocity) /
              (Mathf.Max(ec.maxVelocityMagnitude, ec.minVelocityMagnitude) * ec.velocityGranularity)
            : 1;
        AddReward((-ec.rewDeltaThetaSum  / MaxStep) * ((float) Math.Abs(deltaTheta) / ec.maxDeltaThetaMagnitude) * magVelNormDThetaPunishment);

        // punish for reversing
        if (velocity < 0) AddReward(-ec.rewReverseSum / MaxStep);
        
        // punish for halting
        if (velocity == 0)
        {
            AddReward(-ec.rewHaltSum / MaxStep);
        }


        if (ec.rewNearbyCarsMoveTowards)
        {
            if (gotNearestCarPosBeforeAction)
            {
                var newWorldPos = ModelPosToWorldPos(ComputeNewPosition());
                float dPrev = Vector2.Distance(oldWorldPos,  nearestCarPosBeforeAction);
                float dNew = Vector2.Distance(newWorldPos, nearestCarPosBeforeAction);
                if (Math.Abs(dNew - dPrev) < 0.0001f)
                {
                    // distance to nearest car hasn't changed, don't reward anything
                }
                else if (dNew > dPrev) // distance has increased to nearest car, so have moved away from a nearby car, reward positively
                {
                    AddReward(ec.rewNearbyCarsMoveTowardsSum / MaxStep);
                }
                else // moving towards a nearby car, punish
                {
                    AddReward(-ec.rewNearbyCarsMoveTowardsSum / MaxStep);
                }
            }
        }

        var nearestCar = carTracker.GetNearestCar();
        if (nearestCar == null)
        {
            nearestCarPosBeforeAction = Vector2.zero;
            gotNearestCarPosBeforeAction = false;
        }
        else
        {
            nearestCarPosBeforeAction = nearestCar.gameObject.transform.position;
            gotNearestCarPosBeforeAction = true;
        }
        
        // update goal parking spot
        if (ec.dynamicGoals)
        {
            int goalIndexRaw = actionBuffers.DiscreteActions[2];
            // 0 is explore, i >= 1 is change goal parking spot to nearbyParkingSpots[i - 1]
            ParkingSpace newParkingSpace;
            if (goalIndexRaw == 0) newParkingSpace = null;
            else
            {
                if (goalIndexRaw - 1 >= nearbyParkingSpots.Count) newParkingSpace = parkingSpace;
                else newParkingSpace = nearbyParkingSpots[goalIndexRaw - 1];
            }
            
            UpdateDeltaGoal(parkingSpace, newParkingSpace, true);

            if (newParkingSpace == null)
            {
                SetExploring();
            }
            else
            {
                SetParkingSpace(newParkingSpace);
            }
        }
        
        
        // delta theta and velocity metric
        sr.Add("Metrics/DeltaThetaAvg", Mathf.Abs(deltaTheta), StatAggregationMethod.Average);
        sr.Add("Metrics/VelocityAvg", Mathf.Abs(velocity), StatAggregationMethod.Average);
    }
    
    private static int _numStopExplore = 0;
    private static int _numStopGoal = 0;
    private static int _numLostGoal = 0;
    private static int _numChangeGoal = 0;
    
    // can give way to local or global agent, based upon whether we consider only agents with the same goal or not 
    private static RatioCount _numGiveWayLocalAnyGoal = new RatioCount("Metrics/GaveWayLocalAnyGoal");
    private static RatioCount _numGiveWayGlobalAnyGoal = new RatioCount("Metrics/GaveWayGlobalAnyGoal");
    private static RatioCount _numGiveWayLocalSameGoal = new RatioCount("Metrics/GaveWayLocalSameGoal");
    private static RatioCount _numGiveWayGlobalSameGoal = new RatioCount("Metrics/GaveWayGlobalSameGoal");
    private static RatioCount _numGiveWayNonLocalSameGoal = new RatioCount("Metrics/GaveWayNonLocalSameGoal");
    private static RatioCount _numGiveWayNonLocalAnyGoal = new RatioCount("Metrics/GaveWayNonLocalAnyGoal");

    // applies rewards for delta goal, and optionally updates the metrics
    public void UpdateDeltaGoal(ParkingSpace originalParkingSpace, ParkingSpace newParkingSpace, bool updateMetrics)
    {
        float rew;
        if (originalParkingSpace == null)
        {
            rew = newParkingSpace == null ? ec.rewDeltaGoalContinueExp : ec.rewDeltaGoalStopExp;
            if (updateMetrics && newParkingSpace != null) sr.Add("Metrics/Num Stop Explore", ++_numStopExplore, StatAggregationMethod.MostRecent);
        }
        else
        {
            bool gaveWay = newParkingSpace == null || originalParkingSpace != newParkingSpace;
            var d = Vector2.Distance(gameObject.transform.position, originalParkingSpace.transform.position);
            bool shouldGiveWayLocalAnyGoal = false;
            bool shouldGiveWayGlobalAnyGoal = false;
            bool shouldGiveWayNonLocalAnyGoal = false;
            bool shouldGiveWayLocalSameGoal = false;
            bool shouldGiveWayGlobalSameGoal = false;
            bool shouldGiveWayNonLocalSameGoal = false;
            HashSet<CarAgent> nearbyCarAgents = new HashSet<CarAgent>(
                nearbyCars.Where(c => c.CompareTag("Car")).Select(c => c.GetComponent<CarAgent>()));
            foreach (var carAgent in environmentManager.carAgents)
            {
                if (carAgent == this || Vector2.Distance(carAgent.transform.position, originalParkingSpace.transform.position) >= d) continue;
                bool sameGoal = carAgent.parkingSpace == originalParkingSpace;
                bool local = nearbyCarAgents.Contains(carAgent);
                if (local && sameGoal) shouldGiveWayLocalSameGoal = true;
                if (local) shouldGiveWayLocalAnyGoal = true;
                if (sameGoal) shouldGiveWayGlobalSameGoal = true;
                shouldGiveWayGlobalAnyGoal = true;
                if (!local)
                {
                    if (sameGoal) shouldGiveWayNonLocalSameGoal = true;
                    else shouldGiveWayNonLocalAnyGoal = true;
                }
            }
            if (updateMetrics)
            {
                if (shouldGiveWayLocalAnyGoal) _numGiveWayLocalAnyGoal.Add(gaveWay, sr);
                if (shouldGiveWayGlobalAnyGoal) _numGiveWayGlobalAnyGoal.Add(gaveWay, sr);
                if (shouldGiveWayLocalSameGoal) _numGiveWayLocalSameGoal.Add(gaveWay, sr);
                if (shouldGiveWayGlobalSameGoal) _numGiveWayGlobalSameGoal.Add(gaveWay, sr);
                if (shouldGiveWayNonLocalSameGoal) _numGiveWayNonLocalSameGoal.Add(gaveWay,sr);
                if (shouldGiveWayNonLocalAnyGoal) _numGiveWayNonLocalAnyGoal.Add(gaveWay, sr);
            }
            if (gaveWay)
            {
                bool toExplore = newParkingSpace == null;
                rew = toExplore ? ec.rewDeltaGoalStopGoal : ec.rewDeltaGoalDiffGoal;
                if (updateMetrics) sr.Add(toExplore ? "Metrics/Num Stop Goal" : "Metrics/Num Change Goal", toExplore ? ++_numStopGoal : ++_numChangeGoal, StatAggregationMethod.MostRecent);
            }
            else
            {
                // did not give way
                if (shouldGiveWayLocalSameGoal && ec.punishBetterOtherAgentLocal &&
                    ec.punishBetterOtherGoalAgent
                    || shouldGiveWayGlobalSameGoal && !ec.punishBetterOtherAgentLocal &&
                    ec.punishBetterOtherGoalAgent
                    || shouldGiveWayLocalAnyGoal && ec.punishBetterOtherAgentLocal &&
                    !ec.punishBetterOtherGoalAgent
                    || shouldGiveWayGlobalAnyGoal && !ec.punishBetterOtherAgentLocal &&
                    !ec.punishBetterOtherGoalAgent)
                    rew = ec.rewDeltaGoalContinueGoalBetterOtherAgent;
                else rew = ec.rewDeltaGoalContinueGoal;
            }
        }
        AddReward(ec.rewDeltaGoalWeight * rew);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!envReady) return;
        var discreteActionsOut = actionsOut.DiscreteActions;
        int deltaV = ec.minDeltaVMagnitude;
        int deltaTheta = ec.maxDeltaThetaMagnitude;
        if (Input.GetKey(KeyCode.UpArrow)) deltaV += 1;
        else if (Input.GetKey(KeyCode.DownArrow)) deltaV -= 1;
        else if (Input.GetKey(KeyCode.RightArrow)) deltaTheta += 1;
        else if (Input.GetKey(KeyCode.LeftArrow)) deltaTheta -= 1;

        discreteActionsOut[0] = deltaV;
        discreteActionsOut[1] = deltaTheta;

        if (ec.dynamicGoals)
        {
            int newGoalIndex = parkingSpace == null ? 0 : nearbyParkingSpots.IndexOf(parkingSpace) + 1;
            if (Input.inputString != "") {
                int num;
                bool isNum = Int32.TryParse(Input.inputString, out num);
                if (isNum && num >= 0 && num < 10)
                {
                    newGoalIndex = num;
                }
            }
            discreteActionsOut[2] = newGoalIndex;
        }
    }

    private int Granulate(float value, int granularity)
    {
        return granularity * (int) Math.Floor(value / granularity + 0.5f);
    }

    public void UpdatePosition(Vector2 newPos)
    {
        position = newPos;
        // note newPos is granulated
        var worldPos = ModelPosToWorldPos(newPos);
        gameObject.transform.localPosition = new Vector3(worldPos.x, worldPos.y, -5);
    }

    private Vector2 ModelPosToWorldPos(Vector2 modelPos)
    {
        return new Vector2(mdpToWorldPosScaler * modelPos.x, mdpToWorldPosScaler * modelPos.y);
    }
    
    private Vector2 WorldPosToModelPos(Vector2 worldPos)
    {
        return new Vector2(worldPos.x / mdpToWorldPosScaler, worldPos.y / mdpToWorldPosScaler);
    }

    public void UpdateParkingSpot(bool replaceStaticCar = true)
    {
        lock (_updateParkingSpotLock)
        {
            parkingSpace.RemoveAllGoalAgents();

            var parkingSpots = environmentManager.parkingSpaces;
            // pick a random parking spot that's either inhabited by a static car or free (but not the goal of another agent)
            var availableSpots = parkingSpots.Where(p => !p.GoalCarAgents.Any()).ToList();
            // pick random available spot
            
            // pick closest spot near if near is given, otherwise random
            if (!replaceStaticCar)
            {
                availableSpots = availableSpots.Where(p =>
                    environmentManager.staticCars.All(sc => sc.parkingSpace == null || sc.parkingSpace != p)).ToList();
            }
            ParkingSpace newParkingSpot = Helper.RandomElement(availableSpots);

            // if a static car is in this parking spot, swap it out. place it at a random free spot if a car is in range of the old spot.
            var newParkingSpotStaticCar =
                environmentManager.staticCars.FirstOrDefault(s => s.parkingSpace == newParkingSpot);
            if (newParkingSpotStaticCar == null)
            {
                SetParkingSpace(newParkingSpot);
            }
            else
            {
                parkingSpace.RemoveAllGoalAgents();
                newParkingSpot.RemoveAllGoalAgents();
                newParkingSpot.AddGoalAgent(this);
                // assign the static car in the goal parking spot to the old goal parking spot
                var staticCarParkingSpot = parkingSpace;
                
                var emptySpotCandidates = environmentManager.parkingSpaces.Where(p => !p.GoalCarAgents.Any() && p.InhabitingStaticCar == null).ToList();

                var farEmptySpotCandidates = emptySpotCandidates.Where(p => !InRangeOtherCar(
                    WorldPosToModelPos(p.gameObject.transform.position),
                    ec.carSpawnMinDistance)).ToList();

                staticCarParkingSpot = farEmptySpotCandidates.Contains(staticCarParkingSpot)
                    ? staticCarParkingSpot
                    : (farEmptySpotCandidates.Any() ? Helper.RandomElement(farEmptySpotCandidates) : null);

                if (staticCarParkingSpot == null)
                {
                    newParkingSpot.RemoveAllGoalAgents();
                    UpdateParkingSpot(replaceStaticCar: false);
                    return;
                }
                
                newParkingSpotStaticCar.UpdateParkingSpace(staticCarParkingSpot);
                SetParkingSpace(newParkingSpot);
            }
        }
    }

    public void SetRandomLocation(bool probNonClose = false)
    {
        bool setRandomConflictingParkingSpace = false;
        if (probNonClose)
        {
            var spawnCloseOrRandom = Random.Range(0f, 1f);
            if (spawnCloseOrRandom < ec.spawnCloseRatio)
            {
                if (ec.dynamicGoals)
                {
                    // pick random candidate parking space that is free to spawn near
                    var candidateSpots = environmentManager.parkingSpaces
                        .Where(p => p.InhabitingStaticCar == null && p.GoalCarAgents.Count == 0).ToArray();
                    if (candidateSpots.Length > 0)
                    {
                        var rng = new System.Random();
                        rng.Shuffle(candidateSpots);
                        foreach (var spot in candidateSpots)
                        {
                            if (SpawnClose(spot))
                            {
                                if (Random.Range(0f, 1f) < 0.5f && carTracker.GetNearbyParkingSpots().Contains(spot))
                                {
                                    SetParkingSpace(spot);
                                }
                                _spawnMethod = SpawnMethod.CLOSE_GOAL;
                                return;
                            } 
                        }
                    }
                }
                if (!ec.dynamicGoals && SpawnClose())
                {
                    _spawnMethod = SpawnMethod.CLOSE_GOAL;
                    return;
                }
                // otherwise (originally in range of another car), spawn randomly globally as can't spawn near
            }

            if (spawnCloseOrRandom >= ec.spawnCloseRatio && spawnCloseOrRandom < (ec.spawnCrashRatio + ec.spawnCloseRatio))
            {
                if (ec.dynamicGoals)
                {
                    setRandomConflictingParkingSpace = true;
                }
                else
                {
                    for (var tries = 0; tries < 10; tries++)
                    {
                        if (SpawnCrash())
                        {
                            _spawnMethod = SpawnMethod.CRASH;
                            sr.Add("Metrics/Num Episodes SpawnCrash", ++_numEpisodesSpawnCrash, StatAggregationMethod.MostRecent);
                            return;
                        }
                    }
                }
                // failed to spawn to crash
            }
        }

        _spawnMethod = SpawnMethod.RANDOM;
        // spawn randomly
        var directionalRandomness = (int) (environmentManager.spawnCarArea.localScale.x / mdpToWorldPosScaler) / 2;
        // spawn between -directionalRandomness and directionalRandomness, on both axis
        // thus (0 to 2 * directionalRandomness) - directionalRandomness
        int x;
        int y;
        do
        {
            x = Mathf.RoundToInt(Random.Range(0, 2 * directionalRandomness + 0.4999f)) - directionalRandomness;
            y = Mathf.RoundToInt(Random.Range(0, 2 * directionalRandomness + 0.4999f)) - directionalRandomness;
        } while (InRangeOtherCar(x, y, ec.carSpawnMinDistance)); // check if we're within the threshold distance to other cars, in which case retry

        UpdatePosition(new Vector2(x, y));

        if (setRandomConflictingParkingSpace)
        {
            var candidateParkingSpaces =
                carTracker.GetNearbyParkingSpots().Where(p => p.GoalCarAgents.Count > 0).ToList();
            if (candidateParkingSpaces.Count > 0)
            {
                SetParkingSpace(Helper.RandomElement(candidateParkingSpaces));
                _spawnMethod = SpawnMethod.CRASH;
                sr.Add("Metrics/Num Episodes SpawnCrash", ++_numEpisodesSpawnCrash, StatAggregationMethod.MostRecent);
            }
        }
    }

    private bool SpawnCrash()
    {
        UpdateParkingSpot();
        var spawnAreaCollider = environmentManager.spawnCarArea.GetComponent<Collider2D>();
        var spawnAreaBounds = spawnAreaCollider.bounds;
        var candidateTargetAgents = environmentManager.carAgents.Where(agent => agent != this && agent.parkingSpace != null &&
            agent.ComputeDistanceToGoal(agent.parkingSpace) * maxD >= ec.spawnCrashTargetAgentMinDist).ToList(); // * maxD to remove normalization
        if (candidateTargetAgents.Count == 0) return false;
        var targetAgent = Helper.RandomElement(candidateTargetAgents);
        float targetD = targetAgent.ComputeDistanceToGoal(targetAgent.parkingSpace) * maxD; // * maxD to remove normalization
        // random distance between targetD and spawnCrashTargetAgentMinDist
        Vector2 spawnPos;
        Vector2 spawnPosWorld;
        int tries = 0;
        do
        {
            float upperD = targetD - ec.carSpawnMinDistance;
            if (upperD <= ec.spawnCrashTargetAgentMinDist) return false; // impossible to spawn
            float targetDCollision = Random.Range(ec.spawnCrashTargetAgentMinDist, upperD);

            Vector2 targetGoalV = (targetAgent.position - targetAgent.parkingSpaceModelPos).normalized;
            Vector2 collisionPos = targetAgent.parkingSpaceModelPos + targetGoalV * targetDCollision;

            Vector2 thetaV = (parkingSpaceModelPos - collisionPos).normalized;

            float d = Vector2.Distance(collisionPos, targetAgent.position);

            spawnPos = collisionPos - (thetaV * d);
            spawnPos = new Vector2(Mathf.RoundToInt(spawnPos.x), Mathf.RoundToInt(spawnPos.y));
            spawnPosWorld = ModelPosToWorldPos(spawnPos);
        } while (tries++ < 10 && (InRangeOtherCar((int)spawnPos.x, (int)spawnPos.y, ec.carSpawnMinDistance) 
                 || !spawnAreaBounds.Contains(new Vector3(spawnPosWorld.x, spawnPosWorld.y, spawnAreaCollider.transform.position.z))));

        if (tries > 10)
        {
            return false;
        }
        
        // successful
        UpdatePosition(spawnPos);
        return true;
    }

    private bool SpawnClose(ParkingSpace nearParkingSpace = null)
    {
        if (nearParkingSpace == null) nearParkingSpace = parkingSpace;
        // spawn close
        var spawnAreaBounds = environmentManager.spawnCarArea.GetComponent<Collider2D>().bounds;
        var closestPointToParkingSpaceWorld = spawnAreaBounds
            .ClosestPoint(nearParkingSpace.gameObject.transform.position);
        var randDist = Random.Range(0, ec.spawnCloseDist);
        var modelNewPos = WorldPosToModelPos(closestPointToParkingSpaceWorld);
        while (randDist > 0)
        {
            // random perturbation
            var direction = Random.Range(0, 4);
            var old = new Vector3(closestPointToParkingSpaceWorld.x, closestPointToParkingSpaceWorld.y,
                closestPointToParkingSpaceWorld.z);
            if (direction == 0)
                closestPointToParkingSpaceWorld = new Vector3(closestPointToParkingSpaceWorld.x + 1,
                    closestPointToParkingSpaceWorld.y, closestPointToParkingSpaceWorld.z);
            else if (direction == 1)
                closestPointToParkingSpaceWorld = new Vector3(closestPointToParkingSpaceWorld.x - 1,
                    closestPointToParkingSpaceWorld.y, closestPointToParkingSpaceWorld.z);
            else if (direction == 2)
                closestPointToParkingSpaceWorld = new Vector3(closestPointToParkingSpaceWorld.x,
                    closestPointToParkingSpaceWorld.y + 1, closestPointToParkingSpaceWorld.z);
            else
                closestPointToParkingSpaceWorld = new Vector3(closestPointToParkingSpaceWorld.x,
                    closestPointToParkingSpaceWorld.y - 1, closestPointToParkingSpaceWorld.z);
            modelNewPos = WorldPosToModelPos(closestPointToParkingSpaceWorld);
            if (spawnAreaBounds.Contains(closestPointToParkingSpaceWorld)) randDist--;
            else closestPointToParkingSpaceWorld = old;
        }

        if (InRangeOtherCar((int) modelNewPos.x, (int) modelNewPos.y,
            ec.carSpawnMinDistance)) return false;
        UpdatePosition(WorldPosToModelPos(closestPointToParkingSpaceWorld));
        return true;
    }

    private bool InRangeOtherCar(Vector2 modelXY, float minSqDistance)
    {
        return InRangeOtherCar((int)modelXY.x, (int)modelXY.y, minSqDistance);
    }
    
    private bool InRangeOtherCar(int x, int y, float minSqDistance)
    {
        foreach (var otherCar in environmentManager.carAgents)
        {
            if (otherCar == this) continue;
            float sqDistance = Helper.SquareDistance(x, y, (int) otherCar.position.x, (int) otherCar.position.y);
            if (sqDistance <= minSqDistance)
            {
                return true;
            }
        }
        return false;
    }
}
