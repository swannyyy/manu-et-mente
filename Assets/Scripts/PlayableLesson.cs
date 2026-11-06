using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Five short exercises use real piece selection, legal moves, captures and rotation.
public class PlayableLesson : MonoBehaviour
{
    static PlayableLesson active;
    GameUX ux; game board; Text instruction; int step; bool selected, moved, rotated;
    Chessman focus; Button retry;
    public static bool AllowsSelection(Chessman piece)=>active==null || piece==active.focus;
    public static bool AllowsMove(MovePlate plate)
    {
        if(active==null)return true;
        if(active.step==0||active.step==3)return false;
        if(active.step==2)return plate.attack && plate.GetRingNumber()==1 && plate.GetPlaceNumber()==2;
        if(active.step==4)return plate.GetRingNumber()==7 && plate.GetPlaceNumber()==1;
        return active.focus != null && plate.GetReference()==active.focus.gameObject;
    }
    IEnumerator Start()
    {
        active=this;ux=GetComponent<GameUX>();board=ux.Board;
        ux.PieceSelected+=Selected;ux.MoveCompleted+=Moved;ux.RotationCompleted+=Rotated;
        var panel=UXUI.Panel(ux.Safe,"Practice instructions",UXUI.Surface);UXUI.Place(panel,0,245,0);
        instruction=UXUI.Label(panel,"",8,145,27);
        retry=UXUI.Button(panel,"Restart step",161,ResetStep);
        var r=(RectTransform)retry.transform;r.anchorMax=new Vector2(.5f,1);
        var skip=UXUI.Button(panel,"Exit practice",161,ux.Leave);((RectTransform)skip.transform).anchorMin=new Vector2(.5f,1);
        yield return null;ResetStep();
        while(step<5)
        {
            bool done=step==0?selected:step==3?rotated:moved;
            if(done && !GameUX.BlockInput)
            {
                step++;selected=moved=rotated=false;
                if(step<5){if(step!=1)ResetStep();else ShowInstruction();}
                else Complete();
            }
            yield return null;
        }
    }
    void Selected(Chessman piece){if(piece==focus)selected=true;}
    void Moved(){moved=true;}
    void Rotated(){rotated=true;}
    Chessman Spawn(string type,int ring,int place,string player)
    {
        var cm=board.Create("Practice "+type,type,ring,place,player).GetComponent<Chessman>();board.SetPosition(cm.gameObject);return cm;
    }
    void ResetStep()
    {
        selected=moved=rotated=false;board.PreparePractice();
        // Preserve both teams' commanders to avoid accidental capture victory.
        Spawn("B",step==4?7:1,step==4?2:20,"orange");Spawn("H",1,25,"green");Spawn("B",1,26,"green");
        if(step<=1)focus=Spawn("H",1,1,"orange");
        if(step==2){Spawn("H",1,15,"orange");focus=Spawn("T",1,1,"orange");Spawn("P",1,2,"green");}
        if(step==3){Spawn("H",1,15,"orange");focus=Spawn("S",1,1,"orange");}
        if(step==4)focus=Spawn("H",6,1,"orange");
        ShowInstruction();
    }
    void ShowInstruction()
    {
        instruction.text=new[]{
            "1 / 5 · Select\nTap the Orange Hand. Its name and legal moves will appear below.",
            "2 / 5 · Move\nTap any mint ring to move the Hand. Tap empty space to clear a selection.",
            "3 / 5 · Capture\nSelect the Orange Technician, then tap the adjacent coral ring to capture the Green Pawn.",
            "4 / 5 · Rotate\nSelect the Scholar. Use a curved-arrow control, inspect the preview, then confirm the rotation.",
            "5 / 5 · Win\nSelect the Hand and move it inward to D1. The Mind is already in D: together they win."
        }[step];
    }
    void Complete()
    {
        PlayerPrefs.SetInt("PracticeCompleted",1);PlayerPrefs.Save();
        // Replace the normal victory panel with the lesson completion choices.
        if(board.VictoryPanel!=null)board.VictoryPanel.SetActive(false);
        var p=UXUI.Modal(ux.Safe,"Practice complete");
        UXUI.Label(p,"You selected, moved, captured, rotated, and completed a winning position.\n\nThere are other ways to win: check Winning combinations during a match.",150,280,29);
        UXUI.Button(p,"Play against AI",530,()=>{PlayerPrefs.SetInt("AIDifficulty",0);NetworkController.GoOffline();UnityEngine.SceneManagement.SceneManager.LoadScene("GameAI");},true);
        UXUI.Button(p,"Return to menu",630,ux.Leave);
    }
    void OnDestroy(){if(active==this)active=null;if(ux!=null){ux.PieceSelected-=Selected;ux.MoveCompleted-=Moved;ux.RotationCompleted-=Rotated;}}
}
