using System.Collections.Generic;
using UnityEngine;

// from https://pastebin.com/QCwFCHzJ 

[RequireComponent (typeof (MeshFilter), typeof (MeshRenderer))]
public class CircleOutline : MonoBehaviour {

    public float Radius;
    public float Width;
    public int Resolution;

    // Lists to store generated mesh information
    List<Vector3> vertices = new List<Vector3> ();
    List<int> triangles = new List<int> ();

    // Some sanity checks
    private void OnValidate () {
        if (Radius < 0)
            Radius = 0;
        if (Width < 0)
            Width = 0;
        if (Resolution < 3)
            Resolution = 3;

        // Reform the mesh
        GenerateMesh ();
    }

    public void GenerateMesh () {
        // Clear out existing mesh data
        vertices = new List<Vector3> ();
        triangles = new List<int> ();

        // Angle between two "spokes" of the quad ribbon
        float angleStep = 360f / Resolution;

        for (int i = 0; i < Resolution; i++) {
            // Nasty trig
            // Forms dirA and dirB as spokes from the center using sin(theta), cos(theta)
            Vector3 dirA = (Vector2) transform.position + new Vector2 (Mathf.Sin (i * angleStep * Mathf.Deg2Rad), Mathf.Cos (i * angleStep * Mathf.Deg2Rad));
            Vector3 dirB = (Vector2) transform.position + new Vector2 (Mathf.Sin ((i + 1) * angleStep * Mathf.Deg2Rad), Mathf.Cos ((i + 1) * angleStep * Mathf.Deg2Rad));
            // Form the quad ribbon one section at a time
            AddArcQuad (dirA, dirB, Radius, Width);
        }
        // Apply the mesh to the MeshFilter
        ApplyMesh ();
    }

    void AddArcQuad (Vector2 a, Vector2 b, float radius, float width) {
        int vertexIndex = vertices.Count;
     
        // Multiply a and b by radius for inner vertices
        // Multiply by radius + width for outer vertices
        vertices.Add (a * (radius + width));
        vertices.Add (b * radius);
        vertices.Add (a * radius);
        vertices.Add (b * radius);
        vertices.Add (a * (radius + width));
        vertices.Add (b * (radius + width));

        // Add the triangle info
        triangles.Add (vertexIndex);
        triangles.Add (vertexIndex + 1);
        triangles.Add (vertexIndex + 2);
        triangles.Add (vertexIndex + 3);
        triangles.Add (vertexIndex + 4);
        triangles.Add (vertexIndex + 5);
    }

    void ApplyMesh () {
        Mesh mesh = new Mesh ();
        mesh.vertices = vertices.ToArray ();
        mesh.triangles = triangles.ToArray ();

        // Slow, and throws errors in the editor, but it works.
        GetComponent<MeshFilter> ().mesh = mesh;
    }
}