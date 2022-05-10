Files:
	/envconfigs - Example PPO YAML configuration files for PPO with fixed and dynamic goals
	optimizer_torch.py - Modified file in our custom (modified) version of ML-Agents (version 0.27.0) which decays the PPO parameters to their base values in the first 80% of training episodes and fixes them in the last 20% (the evaluation zone). Importantly, keeps the learning rate = 0 in the last 20% of episodes. Path in the ML-Agents repo is located at ml-agents/mlagents/ppo/optimizer_torch.py.
	learn.slurm - Example .slurm for training a single model with PPO directly.
	learn-base.slurm - Base .slurm file for gridsearch_ppo.py.
	gridsearch_run.py - Example run of gridsearch_ppo.py.
	

*Important notes*:
	- One must download version 0.27.0 of ML-Agents from https://github.com/Unity-Technologies/ml-agents/releases/tag/release_18 into the ml-agents-custom directory, and replace ml-agents-custom/ml-agents/mlagents/ppo/optimizer_torch.py with our custom version, then run `ml-agents-custom/ml-agents/setup.py install`.
	- We assume one is running these tools on Linux (e.g. on the Department's Batch Compute System).
	- One must run the scripts in a Python virtualenv with the dependencies given in requirements.txt.
	- Usage assumes there is a Linux Server Build of the Unity environment available in the build directory.
	- Examples assume the files are within the ~/diss/ffa-ppo directory, and ml-agents-custom is in ~/diss.
