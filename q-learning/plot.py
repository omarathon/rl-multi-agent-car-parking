import pickle
from typing import Dict, List
import matplotlib.pyplot as plt
import pandas as pd
import sys

file_names = []
legends = []

arg = 1
while not sys.argv[arg].isnumeric():
    s = sys.argv[arg].split("&")
    file_names.append(s[0])
    legends.append(s[1] if len(s) > 1 else "")
    arg += 1

mean_window = int(sys.argv[arg])
eval_ep = int(sys.argv[arg + 1])


run_ep_rewards: List[List[float]] = []

for file_name in file_names:
    # Load the Models back from file
    with open(file_name, 'rb') as file: 
        cumulative_rewards: List[float] = pickle.load(file)
        run_ep_rewards.append(cumulative_rewards)

ax = None
for i in range(len(run_ep_rewards)):
    run_rewards = run_ep_rewards[i]
    df = pd.DataFrame(run_rewards, range(len(run_rewards)))
    df_mva = df.rolling(mean_window).mean()
    # df_smallmva = df.rolling(10).mean()
    ax = df_mva.plot(ax=ax)
    # ax=df_smallmva.plot(ax=ax, legend=1, alpha=0.1)

ax.legend(legends)
ax.set_xlabel("Episode Number")
ax.set_ylabel("Episode Cumulative Reward")

ax = plt.axvline(eval_ep, linestyle='dashed', color='red')
plt.show()