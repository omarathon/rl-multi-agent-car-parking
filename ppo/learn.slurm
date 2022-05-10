#!/bin/bash
#
#SBATCH --job-name=omar-rl-train # Job name for tracking
#SBATCH --partition=cpu-batch  # Partition you wish to use (see above for list)
#SBATCH --cpus-per-task=1      # Number of CPU cores used by your job
#SBATCH --mem=4000             # RAM in MB needed in total (also available --mem-per-cpu)
#SBATCH --time=20:00:00        # Job limited to 12 hours (see sinfo for maximums)
#
#SBATCH --mail-type=END,FAIL,TIME_LIMIT_80 # Events to send email on, remove if you don't want this
#SBATCH --output=joboutput_%j.out # Standard out from your job
#SBATCH --error=joboutput_%j.err  # Standard error from your job

## Execute your program(s) ##
source ~/diss/venv/test-env2/bin/activate
module load cuda
srun chmod -R 750 ~/diss/ffa-ppo/build
srun cd ~/diss/ml-agents-custom/ml-agents
srun python3 -m mlagents.trainers.learn /dcs/18/u1830744/diss/ffa-ppo/env_config_tracking.yaml --env=/dcs/18/u1830744/diss/ffa-ppo/build2/build --run-id=run21 --base-port=9900 --force --debug --no-graphics 