using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
/// <summary>
/// Splash behaviour.
/// SHOWS SPLASH LOGO FOR 2 SECONDS.
/// SPLASH BEHAVIOUR FOR DEMO PURPOSES.
/// </summary>
public class SplashBehaviour : MonoBehaviour {
	public string sceneName	=	"";
	// Use this for initialization
	void OnEnable () {
		StartCoroutine(WaitAndLoad(2.0f));
	}

	// every 2 seconds perf
	private IEnumerator WaitAndLoad(float waitTime)
	{
		yield return new WaitForSeconds(waitTime);
		SceneManager.LoadScene (sceneName);
			
	}
}
