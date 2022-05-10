#!/bin/bash
#
#SBATCH --job-name=omar-rl-train- # Job name for tracking
#SBATCH --partition=cpu-batch  # Partition you wish to use (see above for list)
#SBATCH --cpus-per-task=1      # Number of CPU cores used by your job
#SBATCH --mem=1000             # RAM in MB needed in total (also available --mem-per-cpu)
#SBATCH --time=20:00:00        # Job limited to 12 hours (see sinfo for maximums)
#
#SBATCH --mail-type=END,FAIL,TIME_LIMIT_80 # Events to send email on, remove if you don't want this
#SBATCH --output=joboutput_%j.out # Standard out from your job
#SBATCH --error=joboutput_%j.err  # Standard error from your job

## Execute your program(s) ##
source ~/diss/venv/test-env2/bin/activate
module load cuda
srun chmod -R 750 ~/diss/qlearning_test/builds
srun --unbuffered -N1 --ntasks=1 --exclusive python3 ~/diss/qlearning_test/iql_train.py --build "builds/ffa-history/build" --output "ffa-out/final/r15to4_c3h1_2a" --alpha 0.1 --gamma 0.9 --epsilon 0.3 --alphamin 0.1 --epsmin 0.001 --episodes 1000000 --episodeseval 300000 --dump 200000 --envconfig "{'positionGranularity':1,'velocityGranularity':1,'thetaGranularity':45,'maxVelocityMagnitude':1,'minVelocityMagnitude':1,'distNumVals':12,'ringDiams':[15,14,13,12,11,10,9,8,7,6,5,4],'ringMaxNumObjTrack':3,'ringNumPrevObs':1,'maxDeltaVMagnitude':1,'minDeltaVMagnitude':1,'maxDeltaThetaMagnitude':1,'rewDist':true,'obsDist':false,'obsAngle':true,'obsRings':true,'numAgents':2,'numParkedCars':34,'carSpawnMinDistance':9}" --numagents 2 --workerid 10

## Other examples:
## A Python program
# srun python3 my_program.py
#
## A Python program requiring CUDA:
# module load cuda
# srun python3 my_cuda_program.py
#
## Excutable program (e.g. with shebang or written in C/C++)
# srun ~/myprogram