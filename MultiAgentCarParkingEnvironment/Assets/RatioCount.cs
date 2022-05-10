 using Unity.MLAgents;

 public class RatioCount
{
    private int numPositive;
    private int totalCount;
    private readonly string metricName;

    public RatioCount(string metricName)
    {
        numPositive = 0;
        totalCount = 0;
        this.metricName = metricName;
    }

    public void Add(bool positive, StatsRecorder sr)
    {
        totalCount++;
        if (positive) numPositive++;
        sr.Add($"{metricName}_PositiveCount", numPositive, StatAggregationMethod.MostRecent);
        sr.Add($"{metricName}_TotalCount", totalCount, StatAggregationMethod.MostRecent);
    }
}