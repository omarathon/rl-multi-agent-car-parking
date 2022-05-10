using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;

[System.Serializable]
public class EnvironmentConfig
{
    //public int velocityGranularity;
    public float _positionGranularity = 1;
    public float posSize => 1.0f / (float)Math.Pow(_positionGranularity, 2);
    
    public int velocityGranularity => ToInt(_velocityGranularity);
    public float _velocityGranularity = 1;
    
    public int thetaSize => 360 / ToInt(_thetaGranularity);
    public float _thetaGranularity = 8;

    public float rewReachGoal = 10;
    public float rewCrash = 10;
    
    public int maxVelocityMagnitude => ToInt(_maxVelocityMagnitude); // forward
    public float _maxVelocityMagnitude = 1;
    
    public int minVelocityMagnitude => ToInt(_minVelocityMagnitude); // reversing
    public float _minVelocityMagnitude = 1;

    public int distNumVals; // distance is between 0 and distNumVals - 1
    public List<int> ringDiams;
    public int ringMaxNumObjTrack => ToInt(_ringMaxNumObjTrack);
    public int ringNumPrevObs => ToInt(_ringNumPrevObs);
    public bool ringOnlyWall => ToBool(_ringOnlyWall);


    public bool obsDist => ToBool(_obsDist);
    public bool obsAngle => ToBool(_obsAngle);
    public bool obsRings => ToBool(_obsRings);
    public bool obsXY => ToBool(_obsXY);


    // Action bounds
    public int maxDeltaVMagnitude => ToInt(_maxDeltaVMagnitude); // forward
    public float _maxDeltaVMagnitude = 1;
    
    public int minDeltaVMagnitude => maxDeltaVMagnitude; // reversing
    
    public int maxDeltaThetaMagnitude => ToInt(_maxDeltaThetaMagnitude);
    public float _maxDeltaThetaMagnitude = 1;

    public int numAgents => ToInt(_numAgents);


    public int numParkedCars => ToInt(_numParkedCars);
    public float carSpawnMinDistance = 9;

    public bool normaliseObs => ToBool(_normalizeObs);
    public bool debugObs => ToBool(_debugObs);
    public bool visualiseNearbyCars => ToBool(_visualiseNearbyCars);
    
    public bool obsNearbyCars => ToBool(_obsNearbyCars);
    public int obsNearbyCarsCount => ToInt(_obsNearbyCarsCount);
    public int obsNearbyCarsDiameter => ToInt(_obsNearbyCarsDiameter);

    public bool obsGlobalAngle => ToBool(_obsGlobalAngle);
    public bool obsGoalDeltaPose => ToBool(_obsGoalDeltaPose);
    

    // serializable
    public float _numAgents = 1;
    public float _normalizeObs = 0;
    public float _debugObs = 0;
    public float _visualiseNearbyCars = 0;
    public float _numParkedCars = 0;
    
    public float _ringMaxNumObjTrack = 0;
    public float _ringNumPrevObs = 0;
    public float _ringOnlyWall = 0;
    
    // _rd0, _rd1, ..., _rdn = diameter of rings 1 to n, manually injected into ringDiams in EnvironmentManager

    public float _obsDist = 0;
    public float _obsAngle = 1;
    public float _obsRings = 0;
    public float _obsXY = 0;
    public float _obsGoalDeltaPose = 0;

    public float _obsGlobalAngle = 0;

    public float _obsNearbyCars = 0;
    public float _obsNearbyCarsCount = 0;
    public float _obsNearbyCarsDiameter = 0;
    public float _obsNearbyCarsParked = 0;
    public float _obsNearbyCarsGoal = 0;
    public float _obsNearbyCarsVelocity = 0;
    public float _obsNearbyCarsPoseHalf = 1f;

    public bool obsNearbyCarsParked => ToBool(_obsNearbyCarsParked);
    public bool obsNearbyCarsGoal => ToBool(_obsNearbyCarsGoal);

    public bool obsNearbyCarsVelocity => ToBool(_obsNearbyCarsVelocity);

    public bool obsNearbyCarsPoseHalf => ToBool(_obsNearbyCarsPoseHalf);

    public float spawnCloseRatio = 0;
    public float spawnCrashRatio = 0;
    public float spawnCrashTargetAgentMinDist = 10;
    
    public float _spawnCloseDist = 0;
    public int spawnCloseDist => ToInt(_spawnCloseDist);
    
    public float rewFinalPoseSum = 0;

    public float rewDeltaThetaSum = 0;
    public float _rewDeltaThetaVelMult = 0;
    public bool rewDeltaThetaVelMult => ToBool(_rewDeltaThetaVelMult);
    

    public float _maxSteps = 85;
    public int maxSteps => ToInt(_maxSteps);

    public float rewCrashVelocitySum;

    public int rewNearbyCarsDistNumCars => ToInt(_rewNearbyCarsDistNumCars);
    
    public float _rewNearbyCarsDistNumCars = 0;
    public float rewNearbyCarsDistSum = 0;

    public float _rewNearbyCarsMoveTowards = 0;
    public float rewNearbyCarsMoveTowardsSum = 0;
    public bool rewNearbyCarsMoveTowards => ToBool(_rewNearbyCarsMoveTowards);
    
    public float rewReverseSum = 0;

    public float _obsTimestep = 0;
    public bool obsTimestep => ToBool(_obsTimestep);

    public float _rewHalt = 0;
    public bool rewHalt => ToBool(_rewHalt);
    
    public float rewDenseDeltaGoalPoseSum = 0;

    public float _obsNearbyParkingSpotsCount = 0;
    public int obsNearbyParkingSpotsCount => ToInt(_obsNearbyParkingSpotsCount);

    public float _dynamicGoals = 0;
    public bool dynamicGoals => ToBool(_dynamicGoals);

    public int numParkedCarsSecond => ToInt(_numParkedCarsSecond);
    public float _numParkedCarsSecond = -1f;

    public float rewDeltaGoalContinueExp = 0;
    public float rewDeltaGoalStopExp = 0;
    public float rewDeltaGoalContinueGoal = 0;
    public float rewDeltaGoalDiffGoal = 0;
    public float _rewDeltaGoalStopGoal = 0;

    public float rewDeltaGoalStopGoal =>
        Math.Abs(_rewDeltaGoalStopGoal - (-1)) < 0.01f
            ? rewDeltaGoalDiffGoal - rewDeltaGoalStopExp
            : _rewDeltaGoalStopGoal; // potentially compute it as g->g' - e->g if rew is -1 (indicator) 
    
    public float rewDeltaGoalContinueGoalBetterOtherAgent = 0;

    public float _rewDeltaGoalComputeStopGoal;

    public float _obsGoalOneHot = 0;
    public bool obsGoalOneHot => ToBool(_obsGoalOneHot);

    public float rewDeltaGoalWeight = 1;

    public float rewTimeSum = 1;

    public float rewDistSum = 0.5f;

    public float rewHaltSum;


    public float _obsParkingSpotClosestAgent;
    public float _obsParkingSpotClosestGoalAgent;
    public bool obsParkingSpotClosestAgent => ToBool(_obsParkingSpotClosestAgent);
    public bool obsParkingSpotClosestGoalAgent => ToBool(_obsParkingSpotClosestGoalAgent);

    public float _punishBetterOtherAgentLocal;
    public float _punishBetterOtherGoalAgent;

    public bool punishBetterOtherAgentLocal => ToBool(_punishBetterOtherAgentLocal);
    public bool punishBetterOtherGoalAgent => ToBool(_punishBetterOtherGoalAgent);

    public float _obsThetaCorrector = 0;
    public bool obsThetaCorrector => ToBool(_obsThetaCorrector);

    public float _numStepsTrain;
    public int numStepsTrain => ToInt(_numStepsTrain);

    public float carScaleTrain = 1;

    public float _obsNearbyCarsParkedCount = -1;
    public float _obsNearbyCarsAgentCount = -1;
    public int obsNearbyCarsParkedCount => ToInt(_obsNearbyCarsParkedCount);
    public int obsNearbyCarsAgentCount => ToInt(_obsNearbyCarsAgentCount);

    public float rewFinalVelocitySum;

    public int NumCarsTrack => obsNearbyCarsAgentCount == -1
        ? obsNearbyCarsCount
        : obsNearbyCarsAgentCount + obsNearbyCarsParkedCount;

    public int[] ComputeDiscreteObsRanges()
    {
        int[] obs = new int[ComputeNumObs()];
        int obsI = 0;

        // velocity
        obs[obsI++] = maxVelocityMagnitude + minVelocityMagnitude + 1; // plus 1 for 0

        if (obsDist) obs[obsI++] = distNumVals; // distances can be from 0 to distNumVals - 1
        if (obsAngle) obs[obsI++] = 360 / thetaSize;
        
        if (obsRings)
        {
            // observation for each ring and historical ring.
            for (int ri = 0; ri < ringDiams.Count * (ringNumPrevObs + 1); ri++)
            {
                obs[obsI++] = ringMaxNumObjTrack + 1; // can track at most this many objects, + 1 for 0
            }
        }
        
        return obs;
    }

    public int[] ComputeDiscreteActionRanges()
    {
        // first action is deltaV, second action is deltaTheta
        List<int> actionsRanges = new List<int>(new int[]
            {minDeltaVMagnitude + maxDeltaVMagnitude + 1, maxDeltaThetaMagnitude * 2 + 1});
        if (dynamicGoals) actionsRanges.Add(obsNearbyParkingSpotsCount + 1); // 1 for exploring
        return actionsRanges.ToArray();
    }

    public int ComputeNumObsParkingSpace()
    {
        return (obsGoalDeltaPose ? 1 : 0) + (obsDist ? 1 : 0) + (obsXY ? 2 : 0) +
               (obsAngle ? 1 : 0) + (obsParkingSpotClosestAgent ? 1 : 0) + (obsParkingSpotClosestGoalAgent ? 1 : 0);
    }

    public int ComputeNumObs()
    {
        int nearbyCarsNumObs = 0;
        if (obsNearbyCars)
        {
            int numObsPerCar = 3 + (obsNearbyCarsParked ? 1 : 0) + (obsNearbyCarsGoal ? (dynamicGoals ? (obsGoalOneHot ? obsNearbyParkingSpotsCount + 1 : 1) : 2) : 0) + (obsNearbyCarsVelocity ? 1 : 0);
            nearbyCarsNumObs = NumCarsTrack * numObsPerCar;
        }
        int numObsParkingSpaces = (dynamicGoals ? obsNearbyParkingSpotsCount : 1) * ComputeNumObsParkingSpace();
        // 1 for dynamic goals for parking space id (or exploring)
        return 1 + (dynamicGoals ? (obsGoalOneHot ? obsNearbyParkingSpotsCount + 1 : 1) : 0) + (obsTimestep ? 1 : 0) + (obsThetaCorrector ? 1 : 0) + (obsGlobalAngle ? 1 : 0) + numObsParkingSpaces + nearbyCarsNumObs  + (obsRings ? ringDiams.Count * (ringNumPrevObs + 1) : 0);
    }


    private static bool ToBool(float x)
    {
        return Convert.ToBoolean(x);
    }

    private static int ToInt(float x)
    {
        return Mathf.RoundToInt(x);
    }
}