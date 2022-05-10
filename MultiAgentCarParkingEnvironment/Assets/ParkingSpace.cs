using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

public class ParkingSpace : MonoBehaviour
{
    [SerializeField] public TMP_Text text;

    public int id;
    public int idnorm;
    
    public HashSet<CarAgent> GoalCarAgents; // car agents with this parking spot as its goal (may be empty)

    public StaticCar InhabitingStaticCar;

    public HashSet<CarAgent> InhabitingCarAgents;
    
    // Start is called before the first frame update
    void OnEnable()
    {
        GoalCarAgents = new HashSet<CarAgent>();
        InhabitingCarAgents = new HashSet<CarAgent>();
    }
    
    void Start()
    {
    }

    public void AddGoalAgent(CarAgent carAgent)
    {
        GoalCarAgents.Add(carAgent);
    }

    public void RemoveGoalAgent(CarAgent carAgent)
    {
        GoalCarAgents.Remove(carAgent);
    }

    public void RemoveAllGoalAgents()
    {
        CarAgent[] oldGoalCarAgents = new CarAgent[GoalCarAgents.Count];
        GoalCarAgents.CopyTo(oldGoalCarAgents);
        foreach (var goalCarAgent in oldGoalCarAgents)
        {
            RemoveGoalAgent(goalCarAgent);
        }
    }
    
    // Update is called once per frame
    void Update()
    {
        text.text = string.Join("\n", GoalCarAgents.Select(c => c.agentIndex.ToString()));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Car"))
        {
            InhabitingCarAgents.Add(other.GetComponent<CarAgent>());
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Car"))
        {
            InhabitingCarAgents.Remove(other.GetComponent<CarAgent>());
        }
    }
}
