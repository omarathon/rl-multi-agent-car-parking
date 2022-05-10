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

# converts a tuple of ints to a comma separated list string of ints: (1,2,3) -> "1,2,3"
def hash_int_tuple(tuple: Tuple) -> str:
    return ",".join(str(int(x)) for x in tuple)

# converts a comma separated string of ints to a tuple: "1,2,3" -> (1,2,3)
def unhash_int_tuple(hashed_tuple: str) -> Tuple[int]:
    split = hashed_tuple.split(",")
    result = []
    for v in split:
      result.append(int(v))
    return tuple(result)

# converts a comma separated string of ints to a numpy tuple of the given dtype
def unhash_int_tuple_nparray(hashed_list: str, dtype: np.dtype):
  result = np.fromstring(hashed_list, dtype=dtype, sep=',')
  return result

    
# Create the StringLogChannel class
class StringLogChannel(SideChannel):
    # callback is the callback function when the environment is ready, being passed the env, state lookup table and action lookup table
    def __init__(self, callback) -> None:
        super().__init__(uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7"))
        self.callback = callback

    def set_env(self, env):
      self.env = env

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

          # fix the observations and actions to conform to the environment
          for bn in self.env.behavior_specs:
            print(f"bn{bn}obs{len(obs)}")
            behspec = self.env.behavior_specs[bn]
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
            actspec = self.env.behavior_specs[bn].action_spec
            actspec = actspec._replace(discrete_branches=act)
            behspec = behspec._replace(action_spec=actspec)
            behspec = behspec._replace(observation_specs=[os])
            self.env._env_specs[bn] = behspec

          
          self.env.reset()
          self.env.step()
          self.callback(self.env, obs, act)
      

    def send_configuration(self, serializedConfig: str) -> None:
      msg = OutgoingMessage()
      msg.write_string(f"config:{serializedConfig}")
      super().queue_message_to_send(msg)
      print("sent config")


class Env:
    def __init__(
        self,
        build: str,
        worker_id: str,
        env_config: str,
        num_agents: int,
        seed: int
    ):
        self.build = build
        self.worker_id = worker_id
        self.env_config = env_config
        self.num_agents = num_agents
        self.seed = seed
    
    def connect(self, callback):
        string_log = StringLogChannel(callback=callback)
        env = UnityEnvironment(file_name=self.build, seed=self.seed, side_channels=[string_log], worker_id=self.worker_id)
        env.reset()
        string_log.set_env(env)
        string_log.send_configuration(self.env_config)
        env.step()
        

