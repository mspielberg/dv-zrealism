using System.Collections;

namespace DvMod.ZRealism
{
    public static class MessageBox
    {
        public static void ShowMessage(string message, bool pauseGame, float delay)
        {
            var canvasSpawner = SingletonBehaviour<CanvasSpawner>.Instance;
            canvasSpawner?.StartCoroutine(Coro(message, pauseGame, delay));
        }

        private static IEnumerator Coro(string message, bool pauseGame, float delay)
        {
            yield return WaitFor.Seconds(delay);
            MenuScreen? menuScreen = SingletonBehaviour<CanvasSpawner>.Instance.CanvasGO.transform.Find("TutorialPrompt")?.GetComponent<MenuScreen>();
            TutorialPrompt? tutorialPrompt = menuScreen?.GetComponentInChildren<TutorialPrompt>(includeInactive: true);
            if (menuScreen != null && tutorialPrompt != null)
            {
                tutorialPrompt.SetText(message);
                SingletonBehaviour<CanvasSpawner>.Instance.Open(menuScreen, pauseGame);
            }
        }
    }
}
