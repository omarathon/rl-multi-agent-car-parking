#!/bin/bash
#
#SBATCH --job-name=marl # Job name for tracking
#SBATCH --partition=cpu-batch  # Partition you wish to use (see above for list)
#SBATCH --cpus-per-task=2      # Number of CPU cores used by your job
#SBATCH --mem=6000             # RAM in MB needed in total (also available --mem-per-cpu)
#SBATCH --time=47:00:00        # Job limited to 47 hours (see sinfo for maximums)
#
#SBATCH --mail-type=END,FAIL,TIME_LIMIT_80 # Events to send email on, remove if you don't want this
#SBATCH --output=joboutput_%j.out # Standard out from your job
#SBATCH --error=joboutput_%j.err  # Standard error from your job

## Execute your program(s) ##
source ~/diss/venv/test-env2/bin/activate
module load cuda
srun chmod -R 750 ~/diss/ffa-ppo/build
srun cd ~/diss/ml-agents-custom/ml-agents
