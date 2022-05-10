import time
from typing import Dict, List, Tuple
from mlagents_envs.base_env import ActionTuple, DecisionSteps, TerminalSteps, ObservationSpec, DimensionProperty, ObservationType
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.logging_util import _set_formatter_for_all_loggers
import numpy as np
from mlagents_envs.side_channel.side_channel import (
    SideChannel,
    IncomingMessage,
    OutgoingMessage,
)
import uuid
import random
import matplotlib.pyplot as plt
import pickle
import sys
import os
import argparse
import threading
import re

import json

from env import Env, hash_int_tuple, unhash_int_tuple, unhash_int_tuple_nparray

# computes all of the possible permutations for a given tuple of element ranges (from 0 to the value non-incusive). (2,1,3) -> [(0,0,0), (1,0,0), (0,0,1), (1,0,1), (0,0,2, (1,0,2))]
# useful for computing all possible states and actions for q learning
def permutations_range_set(ranges: Tuple[int]) -> List[Tuple[int]]:
      if len(ranges) == 0: return [()]
      head = ranges[0]
      tail = tuple([ranges[i] for i in range(1, len(ranges))])
      permuations_tail = permutations_range_set(tail)
      result = []
      for permutation_tail in permuations_tail:
        for i in range(0, head):
          result.append((i, *permutation_tail))
      return result

def compute_permutation_index_lookup_table(ranges: Tuple[int]) -> Dict[str, int]:
    lookup_table: Dict[str, int] = {} # hash to index
    permutations = permutations_range_set(ranges)
    i = 0
    for permutation in permutations:
        key = hash_int_tuple(permutation)
        lookup_table[key] = i
        i += 1
    return lookup_table

# Hyperparameters
parser = argparse.ArgumentParser()
parser.add_argument('--alpha', help='Learning rate.')
parser.add_argument('--gamma', help='Discount factor.')
parser.add_argument('--epsilon', help='Epsilon greedy fraction.')
parser.add_argument('--episodes', help='Number of episodes to train.')
parser.add_argument('--episodeseval', help='Number of episodes to evaluate the agent (no epsilon).')
parser.add_argument('--dump', help='Size of interval of episodes to dump the current results to output.')
parser.add_argument('--build', help='Build file/directory.')
parser.add_argument('--output', help='Output directory.')
parser.add_argument('--workerid', help='Worker ID for training multiple simultaneously.')
parser.add_argument('--alphamin', help='Min value for learning rate.')
parser.add_argument('--epsmin', help='Min value for epsilon.')
parser.add_argument('--envconfig', help='Serialized configuration options for the environment.')
parser.add_argument('--numagents', help="Number of agents.")

args=parser.parse_args()

alpha = float(args.alpha)
alpha_max = alpha
gamma = float(args.gamma)
epsilon = float(args.epsilon)
epsilon_max = epsilon
alpha_min = float(args.alphamin)
epsilon_min = float(args.epsmin)
num_agents = int(args.numagents)
build = str(args.build)

# alpha = 0.8
# gamma = 0.7
# epsilon = 0.1
num_episodes = int(args.episodes)
num_episodes_eval = int(args.episodeseval)
num_dump_episodes = int(args.dump)
worker_id = int(args.workerid)

env_config = str(args.envconfig)

pkl_metrics_filename = "{}/qlearner_metrics.pkl".format(args.output)
pkl_model_filename = "{}/qlearner_model.pkl".format(args.output)
args_filename = "{}/args.json".format(args.output)

os.makedirs(args.output, exist_ok=True)

# dump args
with open(args_filename, 'w') as fp:
  json.dump(args.__dict__, fp)


def run(env: UnityEnvironment, obs: Tuple[int], act: Tuple[int]):
  global alpha
  global epsilon

  state_lookup_table = compute_permutation_index_lookup_table(obs)
  action_lookup_table = compute_permutation_index_lookup_table(act)


  env.reset()


  # create statehash -> index and index -> statehash maps, and same for actions
  statehash_index_map = state_lookup_table
  stateindex_hash_map: Dict[int, str] = {}
  for statehash in statehash_index_map:
      stateindex_hash_map[statehash_index_map[statehash]] = statehash
  actionhash_index_map = action_lookup_table
  actionindex_hash_map: Dict[int, str] = {}
  for actionhash in actionhash_index_map:
      actionindex_hash_map[actionhash_index_map[actionhash]] = actionhash

  behavior_names = env.behavior_specs.keys()

  behavior_name = list(behavior_names)[0]

  spec = env.behavior_specs[behavior_name]

  print(list(behavior_names))


  # For plotting metrics. cumulative reward across each episode
  agent_episode_cumulative_rewards: List[float] = []

  q_table = np.zeros([len(state_lookup_table), len(action_lookup_table)])

  def store_results():
    if os.path.exists(pkl_metrics_filename):
      os.remove(pkl_metrics_filename)
    if os.path.exists(pkl_model_filename):
      os.remove(pkl_model_filename)
    with open(pkl_metrics_filename, 'wb') as file:
      pickle.dump(agent_episode_cumulative_rewards, file)
    with open(pkl_model_filename, 'wb') as file:
      pickle.dump(q_table, file)

  print(f"discrete branches: {spec.action_spec.discrete_branches}")


  cumulative_rewards: Dict[int, float] = {}

  episode = 0
  while episode < (num_episodes + num_episodes_eval):
    # print(f"a{alpha}e{epsilon}")
    decision_steps, terminal_steps = env.get_steps(behavior_name)

    for agent_id_terminated in terminal_steps: # store their results from this round. their episode has already reset and will be handled in the next decision_steps pass
      agent_id_terminated = int(agent_id_terminated)
      ep_reward = cumulative_rewards[agent_id_terminated]
      agent_episode_cumulative_rewards.append(ep_reward)
      print(f"ER {episode} {agent_id_terminated}: {ep_reward}")

      # after each num_dump_episodes, dump the metrics and q table to separate files
      if (episode + 1) % num_dump_episodes == 0:
        print("D {}.".format(episode))
        store_results()
      
      cumulative_rewards[agent_id_terminated] = 0
      episode += 1

      # epsilon is set to 0 in the eval phase, otherwise it's decayed
      if episode >= num_episodes:
        epsilon = 0
      else:
        epsilon = epsilon_max - ((epsilon_max - epsilon_min) / (num_episodes - 1)) * episode

      # decay learning rate
      alpha = alpha_max - ((alpha_max - alpha_min) / (num_episodes - 1)) * episode

    agent_states: Dict[int, int] = {}
    agent_actions: Dict[int, int] = {}

    actions: List[np.ndarray] = []

    for agent_id in decision_steps:
      agent_id = int(agent_id)
      state = decision_steps[agent_id].obs[0].tolist() # eg [0.5239999890327454, -1.6640020608901978, 0.3999999463558197, 1.5999995470046997]
      state_hash = hash_int_tuple(tuple(state))
      state_index = statehash_index_map[state_hash]

      if random.uniform(0, 1) < epsilon:
          action = spec.action_spec.random_action(1).discrete[0].tolist()  # Explore action space
          action_hash = hash_int_tuple(tuple(action))
          action_index = actionhash_index_map[action_hash]
      else:
          # exploit learned values
          action_index = np.argmax(q_table[state_index])
          action_hash = actionindex_hash_map[action_index]
      agent_states[agent_id] = state_index
      agent_actions[agent_id] = action_index
      actions.append(unhash_int_tuple_nparray(action_hash, np.int32))

    at = ActionTuple()
    at.add_discrete(np.array(actions))

    env.set_actions(behavior_name, at)

    #action_hash = hash_list(action.discrete.tolist())

    env.step()

    def update_q_value(agent_id: int, new_steps):
      # update Q-Value for the agent
      old_q_value = q_table[agent_states[agent_id], agent_actions[agent_id]]
      new_state = new_steps[agent_id].obs[0].tolist()

      # print(f"QTU: {json.dumps(new_state)}")

      new_state_hash = hash_int_tuple(tuple(new_state))
      new_state_index = statehash_index_map[new_state_hash]
      next_max_q_value = np.max(q_table[new_state_index])
      reward = new_steps[agent_id].reward
      new_q_value = (1 - alpha) * old_q_value + alpha * (reward + gamma * next_max_q_value)
      q_table[agent_states[agent_id], agent_actions[agent_id]] = new_q_value

      # update cumulative reward
      if agent_id not in cumulative_rewards: cumulative_rewards[agent_id] = 0
      cumulative_rewards[agent_id] += reward

    new_decision_steps, new_terminal_steps = env.get_steps(behavior_name)
    for agent_id in new_decision_steps:
      # if they also have a terminal step, then they must have ended their episode on this round, thus use their decision_step below
      if agent_id in new_terminal_steps:
        continue
      agent_id = int(agent_id)
      update_q_value(agent_id, new_decision_steps)

    for agent_id in new_terminal_steps: # agent may only have a terminal on one step which was their terminal. they are guaranteed thus to have a decision_step with the reset state
      update_q_value(agent_id, new_terminal_steps)

  env.close()

  store_results()

  print("Training after {} episodes ended successfully.".format(num_episodes))

env_wrapper = Env(build, worker_id, env_config, num_agents, random.randint(1, 99999))

env_wrapper.connect(run)