using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
/// <summary>
/// Scene loader behaviour.
/// SCENE LOADER FOR DEMO PURPOSES 
/// </summary>
public class SceneLoaderBehaviour : MonoBehaviour {

	public string sceneName	=	"";
	public void LoadScene()
	{
		SceneManager.LoadScene (sceneName);
	}
}
