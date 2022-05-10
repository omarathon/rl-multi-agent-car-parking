import sys
import yaml
import json
import os
import shutil
import subprocess


# command mlagents-learn
# base yaml
# base slurm file
# data folder
# base port
# base run id
# <yaml key>=<range of values> grid search params. <range of values> schema: "<type>[<v1>,<v2>,...,<vn>]" where type is one of f(loat), i(nt), s(tring), b(ool)

class Parameter:
    def __init__(self, key, values):
        self.key = key
        self.values = values
    
    @staticmethod
    def parse(arg_str):
        key_value = arg_str.split("=")
        key = key_value[0]
        values_str = key_value[1]
        type_str = values_str[0]
        str_values = values_str[2:-1].split(",")
        values = []
        for str_val in str_values:
            values.append(Parameter.cast_value(str_val, type_str))
        return Parameter(key, values)

    def cast_value(value_str, type_str):
        if type_str == "f":
            return float(value_str)
        if type_str == "i":
            return int(value_str)
        if type_str == "b":
            return bool(value_str)
        if type_str == "s":
            return value_str
        return None


args = sys.argv
learn_cmd = args[1]
base_yaml = args[2]
base_slurm = args[3]
base_port = int(args[4])
base_run_id = args[5]
grid_params = []
param_ranges = []
for i in range(6, len(args)):
    param = Parameter.parse(args[i])
    grid_params.append(param)
    param_ranges.append(len(param.values))

def gen_combinations(param_ranges):
    if len(param_ranges) == 0:
        return [[]]
    param_ranges_rest = param_ranges[1:]
    combos_rest = gen_combinations(param_ranges_rest)
    combos = []
    for i in range(param_ranges[0]):
        for combo_rest in combos_rest:
            combo = []
            combo.append(i)
            for x in combo_rest:
                combo.append(x)
            combos.append(combo)
    return combos

param_combos = gen_combinations(param_ranges)
print("num combos: " + str(len(param_combos)))

with open(base_yaml + ".yaml") as f:
    base_yaml_params = yaml.safe_load(f)

def read_yaml_param(param):
    parts = param.split(".")
    param_value = base_yaml_params
    for part in parts:
        param_value = param_value[part]
    return param_value

def write_yaml_param(param, v):
    parts = param.split(".")
    param_value = base_yaml_params
    for pi in range(0, len(parts) - 1):
        param_value = param_value[parts[pi]]
    param_value[parts[len(parts) - 1]] = v

param_original_values = []
for p in range(0, len(grid_params)):
    param_original_values.append(read_yaml_param(grid_params[p].key))

def restore_yaml_original_values():
    for p in range(0, len(grid_params)):
        write_yaml_param(grid_params[p].key, param_original_values[p])


data_folder = f"gridsearch_data/{base_run_id}"
shutil.rmtree(data_folder, ignore_errors=True)
os.makedirs(data_folder)
os.makedirs(data_folder + "/yaml")
os.makedirs(data_folder + "/slurm")

# gen the grid search yaml and slurm files and run them
for c in range(0, len(param_combos)):
    param_combo = param_combos[c]
    # inject parameter combo to yaml file
    for pi in range(0, len(param_combo)):
        pvi = param_combo[pi]
        param = grid_params[pi]
        pv = param.values[pvi]
        pn = param.key
        # write new value
        write_yaml_param(pn, pv)
    
    # gen yaml with these params
    yaml_file_name = data_folder + "/yaml/_g" + str(c) + ".yaml"
    with open(yaml_file_name, "w") as f:
        yaml.dump(base_yaml_params, f)
    restore_yaml_original_values()

    # generate the slurm files
    slurm_file_name = data_folder + "/slurm/" + base_slurm + "_g" + str(c) + ".slurm"
    shutil.copy(base_slurm + ".slurm", slurm_file_name)
    run_id = base_run_id + "/g" + str(c)
    port = base_port + c
    command = learn_cmd + " " + yaml_file_name + " --env=build/build --run-id=" + run_id + " --base-port=" + str(port) + " --force --debug --no-graphics"
    max_steps = int(read_yaml_param("behaviors.CarBehaviour.max_steps"))
    analysis_command = f"python3 ~/diss/ffa-ppo/analysis.py results/{base_run_id} {str(round(max_steps * 0.8))} results/{base_run_id}/analysis.csv {str(len(param_combos))}"
    for param in grid_params:
        analysis_command += f" {param.key}"
    with open(slurm_file_name, 'a') as slurm_file:
        slurm_file.write(command + "\n")
        slurm_file.write("srun cd ~/diss/ffa-ppo\n")
        slurm_file.write(analysis_command + "\n")

    # run the slurm file
    subprocess.run(["sbatch", slurm_file_name])

    print("Began job " + str(c) + ".")

print("\n==Began all jobs.==\n")