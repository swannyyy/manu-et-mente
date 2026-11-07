// SPDX-License-Identifier: MPL-2.0
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MenuUX : MonoBehaviour
{
    // Set when returning from an analysis opened from the statistics page, to reopen that page.
    public static bool OpenStatsOnLoad;
    RectTransform safe,content,online; Text status; MenuController controller;
    Button create,join,quick,cancel; InputField code,nick; string lastStatus="";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install(){ SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
    static void Loaded(Scene s,LoadSceneMode m)
    {
        if(s.name=="MainMenu")new GameObject("Menu UX").AddComponent<MenuUX>();
    }
    void Start()
    {
        controller=FindAnyObjectByType<MenuController>();
        // Replace the fixed world-space menu with one safe-area screen-space menu.
        foreach(var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            if(c.gameObject.scene==gameObject.scene)c.gameObject.SetActive(false);
        if(controller!=null)controller.enabled=false;
        safe=UXUI.Canvas("Menu interface",100); safe.parent.SetParent(transform,false);
        content=UXUI.Panel(safe,"Menu content",Color.clear);
        var source=FindLogo();
        var logo=UXUI.Panel(content,"Orbital monogram",Color.white);
        logo.anchorMin=logo.anchorMax=new Vector2(.5f,1);logo.pivot=new Vector2(.5f,1);
        logo.anchoredPosition=new Vector2(0,-40);logo.sizeDelta=new Vector2(240,240);
        var image=logo.GetComponent<Image>();image.sprite=source;image.preserveAspect=true;image.raycastTarget=false;
        UXUI.Label(content,"MANU ET MENTE",290,52,30);
        UXUI.Label(content,"A game of movement, rotation and strategy",344,56,23);
        UXUI.Button(content,"Play against AI",430,()=>Difficulty(),true);
        UXUI.Button(content,"Two players · Same device",512,()=>controller.StartGame());
        UXUI.Button(content,"Play online",594,()=>Online());
        UXUI.Button(content,"Learn by playing",676,()=>Learn());
        UXUI.Button(content,"Statistics & game analysis",758,()=>StatsUX.Show(content));
        UXUI.Button(content,"Rules",840,()=>UXUI.Rules(content,null));
        UXUI.Button(content,"Sound: "+(AudioListener.volume>0?"On":"Off"),922,ToggleSound);
        if(!Application.isMobilePlatform)UXUI.Button(content,"Quit",1004,()=>controller.QuitGame());
        if(OpenStatsOnLoad){OpenStatsOnLoad=false;StatsUX.Show(content);}
    }
    Sprite FindLogo()
    {
        foreach(var im in Resources.FindObjectsOfTypeAll<Image>())
            if(im.gameObject.scene==gameObject.scene && im.name=="MainMenuLogo")return im.sprite;
        return null;
    }
    void ToggleSound()
    {
        UXSettings.ToggleSound();
        var b=content.Find("Sound: On")??content.Find("Sound: Off");
        if(b!=null)b.GetComponentInChildren<Text>().text="Sound: "+(AudioListener.volume>0?"On":"Off");
    }
    void Learn()
    {
        var p=UXUI.Modal(content,"Learn by playing");
        UXUI.Label(p,"Learn the rules, or sharpen your tactics",130,70);
        UXUI.Button(p,"Tutorial · Learn the rules step by step",260,()=>{NetworkController.GoOffline();SceneManager.LoadScene("GuidedLearn");},true);
        UXUI.Button(p,"Puzzles · Find the best move",350,()=>{NetworkController.GoOffline();PuzzleUX.Requested=true;SceneManager.LoadScene("Game");});
        int solved=PlayerPrefs.GetInt(PuzzleUX.KeySolved,0),tried=PlayerPrefs.GetInt(PuzzleUX.KeyAttempted,0);
        if(tried>0)UXUI.Label(p,"Puzzles solved first try: "+solved+" / "+tried+" · best streak "+PlayerPrefs.GetInt(PuzzleUX.KeyBest,0),440,60,23);
        UXUI.Button(p,"Back",600,()=>Destroy(p.gameObject));
    }
    void Difficulty()
    {
        var p=UXUI.Modal(content,"Play against AI");
        UXUI.Label(p,"Choose your opponent",130,70);
        UXUI.Button(p,"Easy · Learn at your pace",260,()=>controller.SelectDifficulty(AIAgent.DifficultyLevel.Easy));
        UXUI.Button(p,"Medium · Plan ahead",350,()=>controller.SelectDifficulty(AIAgent.DifficultyLevel.Medium));
        UXUI.Button(p,"Hard · A deeper challenge",440,()=>controller.SelectDifficulty(AIAgent.DifficultyLevel.Hard));
        UXUI.Button(p,"Back",600,()=>Destroy(p.gameObject));
    }
    void Online()
    {
        if(!PlayerProfile.HasProfile)
        {
            var p=UXUI.Modal(content,"Choose a player name");
            nick=UXUI.Input(p,"Your name",180,20);
            var error=UXUI.Label(p,"",270,75,24);
            UXUI.Button(p,"Continue",380,()=>{
                if(string.IsNullOrWhiteSpace(nick.text)){error.text="Enter a name to continue.";return;}
                PlayerProfile.Create(nick.text.Trim());Destroy(p.gameObject);Online();
            },true);
            UXUI.Button(p,"Back",470,()=>Destroy(p.gameObject));return;
        }
        lastStatus=NetworkController.StatusMessage;
        online=UXUI.Modal(content,"Play online");
        UXUI.Label(online,PlayerProfile.Nickname+" · "+PlayerProfile.Points+" points",100,60,25);
        status=UXUI.Label(online,"Create a room, enter a friend's code, or find an opponent.",180,100,25);
        quick=UXUI.Button(online,"Find an opponent",310,()=>controller.QuickMatchGame(),true);
        create=UXUI.Button(online,"Create a room",400,()=>controller.CreateOnlineGame());
        code=UXUI.Input(online,"Room code",510,4);code.contentType=InputField.ContentType.Alphanumeric;
        controller.joinCodeInput=code;
        join=UXUI.Button(online,"Join room",600,()=>{
            if(code.text.Trim().Length!=4){status.text="Enter the four-character room code.";return;}
            controller.JoinOnlineGame();
        });
        cancel=UXUI.Button(online,"Cancel search",700,()=>controller.CancelSearch());cancel.gameObject.SetActive(false);
        UXUI.Button(online,"Back",820,()=>{controller.CancelSearch();Destroy(online.gameObject);});
    }
    void Update()
    {
        if(content!=null){content.anchorMin=content.anchorMax=new Vector2(.5f,.5f);content.sizeDelta=new Vector2(Mathf.Min(660,safe.rect.width),safe.rect.height);}
        if(online==null)return;
        bool searching=NetworkController.IsMatchmaking;
        cancel.gameObject.SetActive(searching);create.interactable=join.interactable=quick.interactable=!searching;
        if(NetworkController.StatusMessage!=lastStatus){lastStatus=NetworkController.StatusMessage;status.text=lastStatus;}
    }
}
public static class UXSettings
{
    [RuntimeInitializeOnLoadMethod]static void Load(){AudioListener.volume=PlayerPrefs.GetInt("SoundEnabled",1);}
    public static void ToggleSound(){bool on=AudioListener.volume>0;AudioListener.volume=on?0:1;PlayerPrefs.SetInt("SoundEnabled",on?0:1);PlayerPrefs.Save();}
}
