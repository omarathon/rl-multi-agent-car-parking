using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProximityRing : MonoBehaviour
{
    [SerializeField] public int diameter;

    private HashSet<Collider2D> colliders = new HashSet<Collider2D>();
 
    public HashSet<Collider2D> GetColliders() { return colliders; }
  
    private void OnTriggerEnter2D (Collider2D other) {
        colliders.Add(other); //hashset automatically handles duplicates
    }
  
    private void OnTriggerExit2D (Collider2D other) {
        colliders.Remove(other);
    }
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.localScale = new Vector3(diameter, diameter, 1);
    }
}
