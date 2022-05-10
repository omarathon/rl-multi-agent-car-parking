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
from numpy.core.arrayprint import dtype_is_implied

from numpy.core.einsumfunc import _compute_size_by_dict

import json
import statistics



# Hyperparameters
parser = argparse.ArgumentParser()
parser.add_argument('--modelpath', help='Path to model')
parser.add_argument('--episodes', help='Number of episodes to simulate.')
parser.add_argument('--envconfig', help='Serialized configuration options for the environment.')
parser.add_argument('--numagents', help="Number of agents.")
parser.add_argument('--build', help='Build path')

args=parser.parse_args()

model_path = args.modelpath

num_agents = int(args.numagents)

num_episodes = int(args.episodes)

env_config = str(args.envconfig)


def hash_int_tuple(tuple: Tuple) -> str:
    return ",".join(str(int(x)) for x in tuple)

def unhash_int_tuple(hashed_tuple: str) -> Tuple[int]:
    split = hashed_tuple.split(",")
    result = []
    for v in split:
      result.append(int(v))
    return tuple(result)

def unhash_int_tuple_nparray(hashed_list: str, dtype: np.dtype):
  result = np.fromstring(hashed_list, dtype=dtype, sep=',')
  return result


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


def run(env: UnityEnvironment, state_lookup_table: Dict[str, int], action_lookup_table: Dict[str, int]):
  env.step()
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


  # For plotting metrics. cumulative reward across each episode for each agent
  agent_episode_cumulative_rewards: List[float] = []

  # Load the Model back from file
  with open(model_path, 'rb') as file:  
    q_table = pickle.load(file)

  print(f"discrete branches: {spec.action_spec.discrete_branches}")

  episode = 0

  cumulative_rewards: Dict[int, float] = {}
  
  while episode < num_episodes:
    decision_steps, terminal_steps = env.get_steps(behavior_name)

    for agent_id_terminated in terminal_steps: # store their results from this round. their episode will reset on the next step of the environment
      ep_reward = cumulative_rewards[agent_id_terminated]
      agent_episode_cumulative_rewards.append(ep_reward)
      print(f"ER {episode} {agent_id_terminated}: {ep_reward}")
    
      cumulative_rewards[agent_id_terminated] = 0
      episode += 1


    agent_states: Dict[int, int] = {}
    agent_actions: Dict[int, int] = {}

    actions: List[np.ndarray] = []

    for agent_id in decision_steps:
      state = decision_steps[agent_id].obs[0].tolist() # eg [0.5239999890327454, -1.6640020608901978, 0.3999999463558197, 1.5999995470046997]
      print(json.dumps(state))
      state_hash = hash_int_tuple(tuple(state))
      state_index = statehash_index_map[state_hash]

      # pick action from q-table
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

    def update_reward(agent_id: int, new_steps):
      agent_id = int(agent_id)
      reward = new_steps[agent_id].reward
      # update cumulative reward
      if agent_id not in cumulative_rewards: cumulative_rewards[agent_id] = 0
      cumulative_rewards[agent_id] += reward

    new_decision_steps, new_terminal_steps = env.get_steps(behavior_name)
    for agent_id in new_decision_steps:
      # if they also have a terminal step, then they must have ended their episode on this round, thus use their decision_step below
      if agent_id in new_terminal_steps:
        continue
      update_reward(agent_id, new_decision_steps)
    for agent_id in new_terminal_steps:
      update_reward(agent_id, new_terminal_steps)

  env.close()

  # plot historgram with cumulative rewards if given
  print("Episode rewards:")
  print(json.dumps(agent_episode_cumulative_rewards))
  print("Mean Episode Reward: " + str(statistics.mean(agent_episode_cumulative_rewards)))

  print("{} episodes ended.".format(num_episodes))


# Create the StringLogChannel class
class StringLogChannel(SideChannel):
    def __init__(self) -> None:
        super().__init__(uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7"))

    def set_env(self, env):
      self.env = env

    def compute_permutation_index_lookup_table(self, ranges: Tuple[int]) -> Dict[str, int]:
        lookup_table: Dict[str, int] = {} # hash to index
        permutations = permutations_range_set(ranges)
        i = 0
        for permutation in permutations:
            key = hash_int_tuple(permutation)
            lookup_table[key] = i
            i += 1
        return lookup_table

    def on_message_received(self, msg: IncomingMessage) -> None:
        """
        Note: We must implement this method of the SideChannel interface to
        receive messages from Unity
        """
        # We simply read a string from the message and print it.
        msgstr = msg.read_string()
        print(f"From Unity: {msgstr}")
        if msgstr.startswith("configACK:"):
          # extract obs ranges and action ranges
          obs_s: str = re.findall("obs\[(.*?)\]", msgstr)[0]
          act_s: str = re.findall("act\[(.*?)\]", msgstr)[0]
          obs = unhash_int_tuple(obs_s)
          act = unhash_int_tuple(act_s)
          print(f"OBS: {obs}\nACT: {act}")
          state_lookup_table = self.compute_permutation_index_lookup_table(obs)
          action_lookup_table = self.compute_permutation_index_lookup_table(act)

          for bn in env.behavior_specs:
            print(f"bn{bn}obs{len(obs)}")
            behspec = env.behavior_specs[bn]
            print(f"bs{len(behspec.observation_specs)}")
            os = ObservationSpec(
                name="",
                shape=(len(obs),),
                observation_type=ObservationType.DEFAULT,
                dimension_property=tuple(
                    DimensionProperty.UNSPECIFIED for _ in range(0, len(obs))
                )
            )
            behspec.observation_specs.append(os)
            # bs = [behspec.observation_specs[0]._replace(shape=(len(obs),))]
            actspec = env.behavior_specs[bn].action_spec
            actspec = actspec._replace(discrete_branches=act)
            behspec = behspec._replace(action_spec=actspec)
            behspec = behspec._replace(observation_specs=[os])
            env._env_specs[bn] = behspec

          
          env.reset()
          env.step()
          run(self.env, state_lookup_table, action_lookup_table)
      

    def send_configuration(self, serializedConfig: str) -> None:
      msg = OutgoingMessage()
      msg.write_string(f"config:{serializedConfig}")
      super().queue_message_to_send(msg)
      print("sent config")


string_log = StringLogChannel()
env = UnityEnvironment(file_name=args.build, seed=8, side_channels=[string_log], worker_id=0)
env.reset()
string_log.set_env(env)
string_log.send_configuration(env_config)
env.step()