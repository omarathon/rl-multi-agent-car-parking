
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator
from pathlib import Path
import json

import sys
import yaml
import statistics
import csv

import os.path


# want to know: 
#               run id
#               run parameters from yaml
# 
#               final mean reward (across last 15 summaries)
#               final mean episode length (across last 15 summaries)
#               final num crashes
#               final num parks
#               final num halts
#               final num episodes

class RunMetrics:
    def __init__(self, run_id, metric_values):
        self.run_id = run_id
        self.metric_values = metric_values

    def __str__(self):
        return f"run_id: {self.run_id}\nmetric_values: {json.dumps(self.metric_values)}\n"

def tabulate_events(dpath):

    # list of RunMetrics
    results = []

    def process(dnamep):
        dname = str(dnamep)
        print(f"Converting run {dnamep}",end="")

        ea = EventAccumulator(dname).Reload()
        tags = ea.Tags()['scalars']

        # map of scalar to array of values across all steps
        scalar_values = {}

        metric_period = int(sys.argv[2])
        

        aggr_metrics_event_indexes = {}

        max_steps = -1
        for tag in tags:
            values = []
            found_begin_aggr_metrics = False
            for ei, event in enumerate(ea.Scalars(tag)):
                step = int(event.step)
                if not found_begin_aggr_metrics and step >= metric_period:
                    aggr_metrics_event_indexes[tag] = ei
                    found_begin_aggr_metrics = True
                values.append(event.value)
                max_steps = max(max_steps, step)
            scalar_values[str(tag)] = values

        metric_values = {}

        # extract metrics from yaml
        yamlp = str(dnamep.parent.parent) + "/configuration.yaml"
        with open(yamlp) as f:
            base_yaml_params = yaml.safe_load(f)
        
            def read_yaml_param(param):
                env_param = param.startswith("environment_parameters")
                parts = param.split(".")
                param_value = base_yaml_params
                for part in parts:
                    param_value = param_value[part]

                if env_param:
                    param_value = param_value["curriculum"][0]["value"]["sampler_parameters"]["value"]

                return param_value

            for argi in range(5,len(sys.argv)):
                yaml_param = sys.argv[argi]
                metric_values[yaml_param] = read_yaml_param(yaml_param)

        def mean_metric_period(tag):
            data = scalar_values[tag]
            if tag not in aggr_metrics_event_indexes: return data[len(data) - 1]
            data_last = data[aggr_metrics_event_indexes[tag]:]
            return statistics.mean(data_last)

        def last_minus_start_metricperiod(tag):
            if tag not in aggr_metrics_event_indexes: return 0
            data = scalar_values[tag]
            return data[len(data) - 1] - data[aggr_metrics_event_indexes[tag]]

        def extract_metric(scalar_values_name, compute_fn):
            if scalar_values_name in scalar_values: metric_values[scalar_values_name[len("Metrics/"):]] = compute_fn(scalar_values_name)

        # generate metrics from scalar values
        metric_values["Max Steps"] = max_steps
        metric_values["Final Mean Reward"] = mean_metric_period("Environment/Cumulative Reward")
        metric_values["Final Mean Episode Length"] = mean_metric_period("Environment/Episode Length")

        extract_metric("Metrics/Total Crashes", last_minus_start_metricperiod)
        extract_metric("Metrics/Total Reached Goal", last_minus_start_metricperiod)
        extract_metric("Metrics/Total Halted", last_minus_start_metricperiod)
        extract_metric("Metrics/Num Episodes", last_minus_start_metricperiod)
        extract_metric("Metrics/Total Crashes StartEpisode", last_minus_start_metricperiod)
        extract_metric("Metrics/Total Crashes Car", last_minus_start_metricperiod)
        extract_metric("Metrics/Total Crashes Wall", last_minus_start_metricperiod)
        extract_metric("Metrics/Total Crashes StaticCar", last_minus_start_metricperiod)
        extract_metric("Metrics/NumParkedCars Oscillations", last_minus_start_metricperiod)
        extract_metric("Metrics/Num Lost Goal", last_minus_start_metricperiod)
        extract_metric("Metrics/Num Stop Explore", last_minus_start_metricperiod)
        extract_metric("Metrics/Num Stop Goal", last_minus_start_metricperiod)
        extract_metric("Metrics/Num Change Goal", last_minus_start_metricperiod)

        extract_metric("Metrics/GaveWayLocalAnyGoal_PositiveCount", last_minus_start_metricperiod)
        extract_metric("Metrics/GaveWayGlobalAnyGoal_PositiveCount", last_minus_start_metricperiod)
        extract_metric("Metrics/GaveWayLocalSameGoal_PositiveCount", last_minus_start_metricperiod)
        extract_metric("Metrics/GaveWayGlobalSameGoal_PositiveCount", last_minus_start_metricperiod)
        extract_metric("Metrics/GaveWayNonLocalSameGoal_PositiveCount", last_minus_start_metricperiod)
        extract_metric("Metrics/GaveWayNonLocalAnyGoal_PositiveCount", last_minus_start_metricperiod)

        extract_metric("Metrics/GaveWayLocalAnyGoal_TotalCount", last_minus_start_metricperiod)
        extract_metric("Metrics/GaveWayGlobalAnyGoal_TotalCount", last_minus_start_metricperiod)
        extract_metric("Metrics/GaveWayLocalSameGoal_TotalCount", last_minus_start_metricperiod)
        extract_metric("Metrics/GaveWayGlobalSameGoal_TotalCount", last_minus_start_metricperiod)
        extract_metric("Metrics/GaveWayNonLocalSameGoal_TotalCount", last_minus_start_metricperiod)
        extract_metric("Metrics/GaveWayNonLocalAnyGoal_TotalCount", last_minus_start_metricperiod)

        extract_metric("Metrics/DeltaThetaAvg", mean_metric_period)
        extract_metric("Metrics/VelocityAvg", mean_metric_period)
        extract_metric("Metrics/NearestCarDistAvg", mean_metric_period)

        extract_metric("Metrics/RatioExplorePerEps", mean_metric_period)
        extract_metric("Metrics/RatioGoalPerEps", mean_metric_period)
        extract_metric("Metrics/RatioMoveTowardsPerEps", mean_metric_period)
        extract_metric("Metrics/RatioMoveTowardsExploringPerEps", mean_metric_period)
        extract_metric("Metrics/Park Velocity", mean_metric_period)

        # if "Metrics/Total Crashes" in scalar_values: metric_values["Final Num Crashes"] = last_minus_start_metricperiod("Metrics/Total Crashes")
        # if "Metrics/Total Reached Goal" in scalar_values: metric_values["Final Num Parks"] = last_minus_start_metricperiod("Metrics/Total Reached Goal")
        # if "Metrics/Total Halted" in scalar_values: metric_values["Final Num Halts"] = last_minus_start_metricperiod("Metrics/Total Halted")
        # if "Metrics/Num Episodes" in scalar_values: metric_values["Final Num Episodes"] = last_minus_start_metricperiod("Metrics/Num Episodes")

        # if "Metrics/Total Crashes StartEpisode" in scalar_values: metric_values["Final Num Crashes StartEpisode"] = last_minus_start_metricperiod("Metrics/Total Crashes StartEpisode")
        # if "Metrics/Total Crashes Car" in scalar_values: metric_values["Final Num Crashes Car"] = last_minus_start_metricperiod("Metrics/Total Crashes Car")
        # if "Metrics/Total Crashes Wall" in scalar_values: metric_values["Final Num Crashes Wall"] = last_minus_start_metricperiod("Metrics/Total Crashes Wall")
        # if "Metrics/Total Crashes StaticCar" in scalar_values: metric_values["Final Num Crashes StaticCar"] = last_minus_start_metricperiod("Metrics/Total Crashes StaticCar")
        # if "Metrics/NumParkedCars Oscillations" in scalar_values: metric_values["Final Num Oscillations"] = last_minus_start_metricperiod("Metrics/NumParkedCars Oscillations")

        # if "Metrics/Num Lost Goal" in scalar_values: metric_values["Final Num Lost Goal"] = last_minus_start_metricperiod("Metrics/Num Lost Goal")
        # if "Metrics/Num Stop Explore" in scalar_values: metric_values["Final Num Stop Explore"] = last_minus_start_metricperiod("Metrics/Num Stop Explore")
        # if "Metrics/Num Stop Goal" in scalar_values: metric_values["Final Num Stop Goal"] = last_minus_start_metricperiod("Metrics/Num Stop Goal")
        # if "Metrics/Num Change Goal" in scalar_values: metric_values["Final Num Change Goal"] = last_minus_start_metricperiod("Metrics/Num Change Goal")

        def div(a,b):
            if b == 0: return -1
            return a / b

        # compute p, c, h
        if "Metrics/Num Episodes" in scalar_values:
            e = metric_values["Num Episodes"]
            if "Metrics/Total Reached Goal" in scalar_values: metric_values["p"] = div(metric_values["Total Reached Goal"], e)
            if "Metrics/Total Crashes" in scalar_values: metric_values["c"] = div(metric_values["Total Crashes"], e)
            if "Metrics/Total Halted" in scalar_values: metric_values["h"] = div(metric_values["Total Halted"], e)

            if "Metrics/Num Lost Goal" in scalar_values: metric_values["NumLostGoal PerEps"] = div(metric_values["Num Lost Goal"], e)
            if "Metrics/Num Stop Explore" in scalar_values: metric_values["NumStopExplore PerEps"] = div(metric_values["Num Stop Explore"], e)
            if "Metrics/Num Stop Goal" in scalar_values: metric_values["NumStopGoal PerEps"] = div(metric_values["Num Stop Goal"], e)
            if "Metrics/Num Change Goal" in scalar_values: metric_values["NumChangeGoal PerEps"] = div(metric_values["Num Change Goal"], e)
            
            
        if "Metrics/GaveWayLocalAnyGoal_PositiveCount" in scalar_values: metric_values["GaveWayLocalAnyGoal"] = div(metric_values["GaveWayLocalAnyGoal_PositiveCount"], metric_values["GaveWayLocalAnyGoal_TotalCount"])
        if "Metrics/GaveWayGlobalAnyGoal_PositiveCount" in scalar_values: metric_values["GaveWayGlobalAnyGoal"] = div(metric_values["GaveWayGlobalAnyGoal_PositiveCount"], metric_values["GaveWayGlobalAnyGoal_TotalCount"])
        if "Metrics/GaveWayLocalSameGoal_PositiveCount" in scalar_values: metric_values["GaveWayLocalSameGoal"] = div(metric_values["GaveWayLocalSameGoal_PositiveCount"], metric_values["GaveWayLocalSameGoal_TotalCount"])
        if "Metrics/GaveWayGlobalSameGoal_PositiveCount" in scalar_values: metric_values["GaveWayGlobalSameGoal"] = div(metric_values["GaveWayGlobalSameGoal_PositiveCount"], metric_values["GaveWayGlobalSameGoal_TotalCount"])
        if "Metrics/GaveWayNonLocalSameGoal_PositiveCount" in scalar_values: metric_values["GaveWayNonLocalSameGoal"] = div(metric_values["GaveWayNonLocalSameGoal_PositiveCount"] , metric_values["GaveWayNonLocalSameGoal_TotalCount"])
        if "Metrics/GaveWayNonLocalAnyGoal_PositiveCount" in scalar_values: metric_values["GaveWayNonLocalAnyGoal"] = div(metric_values["GaveWayNonLocalAnyGoal_PositiveCount"], metric_values["GaveWayNonLocalAnyGoal_TotalCount"])


        # get run id
        run_id = dnamep.parent.parent.name
        results.append(RunMetrics(run_id, metric_values))
        print(" ... DONE")

    expected_num_models = int(sys.argv[4])
    if len(list(Path(dpath).rglob("CarBehaviour.onnx"))) != expected_num_models:
        return None
    lock_path = f"{dpath}/analysis.lck"
    if os.path.exists(lock_path):
        return None
    f = open(lock_path, "w")
    f.close()

    for path in list(Path(dpath).rglob("*.tfevents.*")):
        process(path)
    
    return results

if __name__ == '__main__':
    path = sys.argv[1]
    results = tabulate_events(path)
    if (results is None):
        print("\n\nNO ANALYSIS RESULTS - not enough models")
    else:
        print("\n\nRESULTS:\n")
        for result in results:
            print(result)

        keys = {"run_id"}
        for result in results:
            for metric in result.metric_values.keys():
                keys.add(metric)
        
        keys = sorted(keys)

        out_file = sys.argv[3]

        with open(out_file, "w", newline='') as output_file:
            dict_writer = csv.DictWriter(output_file, keys)
            dict_writer.writeheader()
            rows = []
            for run_metrics in results:
                map = run_metrics.metric_values.copy()
                map["run_id"] = run_metrics.run_id
                for key in keys:
                    if key not in map:
                        map[key] = 0
                rows.append(map)
            dict_writer.writerows(rows)