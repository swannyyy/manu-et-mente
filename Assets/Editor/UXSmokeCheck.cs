#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Run from an existing Play session. Exercises real legal moves and the five lesson tasks.
public static class UXSmokeCheck
{
    static IEnumerator routine; static string stage; static double deadline; static int lastFrame;
    static bool hadAI,hadPractice; static int oldAI,oldPractice;
    [MenuItem("Tools/Manu et Mente/Run UX smoke checks %#u")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying){Debug.LogError("Enter Play Mode before running UX checks.");return;}
        hadAI=PlayerPrefs.HasKey("AIDifficulty");oldAI=PlayerPrefs.GetInt("AIDifficulty");
        hadPractice=PlayerPrefs.HasKey("PracticeCompleted");oldPractice=PlayerPrefs.GetInt("PracticeCompleted");
        lastFrame=-1;routine=Checks();deadline=EditorApplication.timeSinceStartup+90;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out at "+stage);
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(routine.MoveNext())return;
            File.WriteAllText("/tmp/manu-ux-smoke.txt","PASS: lesson selection, movement, capture, rotation preview/confirmation, positional victory, menu logo/rules, AI pause/resume and move/rotation execution, zoom/reset, leave confirmation/cancel, interruption gate.");
            Debug.Log("UX smoke checks PASSED");RestorePreferences();EditorApplication.update-=Tick;
        }
        catch(Exception e){File.WriteAllText("/tmp/manu-ux-smoke.txt","FAIL at "+stage+": "+e);Debug.LogException(e);RestorePreferences();EditorApplication.update-=Tick;}
    }
    static void RestorePreferences()
    {
        if(hadAI)PlayerPrefs.SetInt("AIDifficulty",oldAI);else PlayerPrefs.DeleteKey("AIDifficulty");
        if(hadPractice)PlayerPrefs.SetInt("PracticeCompleted",oldPractice);else PlayerPrefs.DeleteKey("PracticeCompleted");
        PlayerPrefs.Save();
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static IEnumerator Checks()
    {
        stage="load practice";NetworkController.GoOffline();SceneManager.LoadScene("GuidedLearn");
        for(int i=0;i<10;i++)yield return null;
        var ux=GameUX.Instance;Require(ux!=null && ux.Board!=null,"Practice UX missing");var board=ux.Board;
        while(board.GetPosition(1,1)==null)yield return null;
        stage="select Hand";var hand=board.GetPosition(1,1).GetComponent<Chessman>();hand.OnMouseUp();
        Require(ux.Selected==hand,"Selection did not persist");
        for(int i=0;i<5;i++)yield return null;
        stage="move Hand";
        var move=UnityEngine.Object.FindObjectsByType<MovePlate>().First(p=>!p.attack);
        int targetRing=move.GetRingNumber(),targetPlace=move.GetPlaceNumber();move.OnMouseUp();
        while(GameUX.BlockInput)yield return null;
        for(int i=0;i<8;i++)yield return null;
        stage="capture exercise";var tech=board.GetPosition(1,1).GetComponent<Chessman>();
        Require(tech.pieceType=="T","Lesson did not advance after real move");tech.OnMouseUp();yield return null;
        var capture=UnityEngine.Object.FindObjectsByType<MovePlate>().First(p=>p.attack&&p.GetRingNumber()==1&&p.GetPlaceNumber()==2);capture.OnMouseUp();
        while(GameUX.BlockInput)yield return null;for(int i=0;i<8;i++)yield return null;
        stage="rotation preview";var scholar=board.GetPosition(1,1).GetComponent<Chessman>();Require(scholar.pieceType=="S","Capture did not advance lesson");
        scholar.OnMouseUp();yield return null;ux.PreviewRotation(true);
        Require(GameUX.BlockInput,"Preview must block board input");Require(scholar.placeNumber==1,"Preview changed board state");
        var confirm=ux.Safe.GetComponentsInChildren<Button>().First(b=>b.name=="Rotate");confirm.onClick.Invoke();
        while(GameUX.BlockInput)yield return null;for(int i=0;i<8;i++)yield return null;
        stage="winning exercise";hand=board.GetPosition(6,1).GetComponent<Chessman>();hand.OnMouseUp();yield return null;
        var win=UnityEngine.Object.FindObjectsByType<MovePlate>().First(p=>p.GetRingNumber()==7&&p.GetPlaceNumber()==1);win.OnMouseUp();
        while(GameUX.BlockInput)yield return null;for(int i=0;i<8;i++)yield return null;
        Require(board.LastVictoryType==game.VictoryType.HandAndMindInD,"Wrong practice victory");
        Require(ux.Safe.Find("Practice complete")!=null,"No lesson completion UI");
        stage="main menu";SceneManager.LoadScene("MainMenu");for(int i=0;i<10;i++)yield return null;
        var menu=UnityEngine.Object.FindAnyObjectByType<MenuUX>();Require(menu!=null,"Main menu missing");
        var logo=menu.GetComponentsInChildren<Image>().First(i=>i.name=="Orbital monogram");Require(logo.sprite!=null,"Logo reference missing");
        var rules=menu.GetComponentsInChildren<Button>().First(b=>b.name=="Rules");rules.onClick.Invoke();yield return null;
        Require(menu.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Capture both enemy commanders")),"Rules did not open");
        stage="AI pause and resume";PlayerPrefs.SetInt("AIDifficulty",0);SceneManager.LoadScene("GameAI");for(int i=0;i<10;i++)yield return null;
        ux=GameUX.Instance;board=ux.Board;
        var pawn=board.GetPosition(2,27).GetComponent<Chessman>();pawn.OnMouseUp();yield return null;
        move=UnityEngine.Object.FindObjectsByType<MovePlate>().First(p=>!p.attack);move.OnMouseUp();
        while(GameUX.BlockInput)yield return null;
        int responses=0;ux.MoveCompleted+=()=>responses++;ux.RotationCompleted+=()=>responses++;
        ux.ShowMenu();var last=board.GetLastMove();float until=Time.realtimeSinceStartup+3;
        while(Time.realtimeSinceStartup<until)yield return null;
        Require(board.GetLastMove()==last && responses==0,"AI acted through pause menu");ux.CloseModal();
        until=Time.realtimeSinceStartup+12;
        while(board.GetCurrentPlayer()!="orange" && Time.realtimeSinceStartup<until)yield return null;
        Require(board.GetCurrentPlayer()=="orange" && responses==1,"AI did not resume with a legal move or rotation");
        while(GameUX.BlockInput)yield return null;
        stage="zoom and leave confirmation";
        var camera=Camera.main;float originalSize=camera.orthographicSize;ux.ToggleZoom();yield return null;
        Require(camera.orthographicSize<originalSize,"Zoom did not magnify the board");ux.ToggleZoom();yield return null;
        Require(Mathf.Abs(camera.orthographicSize-originalSize)<.001f,"Zoom reset did not fit the board");
        ux.ShowMenu();ux.Safe.GetComponentsInChildren<Button>().First(b=>b.name=="Leave game").onClick.Invoke();yield return null;
        Require(ux.Safe.Find("Leave this game?")!=null,"Leave confirmation missing");
        ux.Safe.GetComponentsInChildren<Button>().First(b=>b.name=="Keep playing").onClick.Invoke();yield return null;
        Require(GameUX.Instance==ux&&!GameUX.BlockInput,"Keep playing did not resume the same match");
        stage="interruption gate";NetworkController.SessionInterrupted=true;Require(GameUX.BlockInput,"Interrupted match accepts input");NetworkController.GoOffline();Require(!GameUX.BlockInput,"Offline mode retained interruption gate");
        ScreenCapture.CaptureScreenshot("/tmp/manu-ux-gameplay.png");
    }
}
#endif
