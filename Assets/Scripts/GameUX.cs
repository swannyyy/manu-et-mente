// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class GameUX : MonoBehaviour
{
    public static GameUX Instance {get;private set;}
    public static bool BlockInput => NetworkController.SessionInterrupted || Instance!=null && (Instance.modal!=null || Instance.animations>0 || Instance.connectionLost || Instance.Reviewing);
    public static bool PauseAI => Instance!=null && (Instance.modal!=null || Instance.animations>0 || Instance.Reviewing);
    // True while the post-game analysis screen is open (board shows replayed positions).
    public bool Reviewing;
    public bool ModalOpen => modal!=null;
    public Chessman Selected {get;private set;}
    public game Board {get;private set;}
    public bool Learning => gameObject.scene.name=="GuidedLearn";
    public event Action<Chessman> PieceSelected;
    public event Action MoveCompleted;
    public event Action RotationCompleted;
    public RectTransform Safe => safe;
    RectTransform safe,header,footer,modal; Text turn,selection,progress,lastAction;
    Button left,right,copy; Camera camera; AIAgent ai;
    LineRenderer selectedLine,fromLine,toLine,ringLine;
    Material lineMaterial; int animations; bool connectionLost; float fitSize; float zoom=1; bool resultShown;
    Vector2 lastPan; bool gesture; bool wasOnline; string observedStatus="";
    float notificationUntil; string notification; string lastDescription="";
    readonly Dictionary<Transform,Coroutine> tweens=new Dictionary<Transform,Coroutine>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
    static void Loaded(Scene s,LoadSceneMode mode)
    {
        if(s.name=="Game"||s.name=="GameAI"||s.name=="GuidedLearn")new GameObject("Gameplay UX").AddComponent<GameUX>();
    }
    void Awake(){Instance=this;}
    void Start()
    {
        Board=FindAnyObjectByType<game>();ai=FindAnyObjectByType<AIAgent>();
        camera=Camera.main??FindAnyObjectByType<Camera>();
        camera.tag="MainCamera";
        foreach(var manager in FindObjectsByType<RotationButtonManager>(FindObjectsInactive.Exclude))
        {
            foreach(var b in new[]{manager.btnAaACW,manager.btnAaCW,manager.btnBbACW,manager.btnBbCW,manager.btnCcACW,manager.btnCcCW,manager.btnDACW,manager.btnDCW})
                if(b!=null)b.gameObject.SetActive(false);
            manager.enabled=false;
        }
        if(Board.BackToMenuBTN!=null)Board.BackToMenuBTN.gameObject.SetActive(false);
        safe=UXUI.Canvas("Gameplay interface",100);safe.parent.SetParent(transform,false);
        header=UXUI.Panel(safe,"Match status",Color.clear);UXUI.Place(header,0,245,0);
        turn=UXUI.Label(header,"",12,65,32);
        progress=UXUI.Label(header,"",78,68,22);
        var objectives=UXUI.Button(header,"Winning combinations",156,ShowObjectives); Half(objectives,false);
        var menu=UXUI.Button(header,"Menu",156,ShowMenu);Half(menu,true);
        footer=UXUI.Panel(safe,"Piece controls",UXUI.Surface);
        footer.anchorMin=Vector2.zero;footer.anchorMax=new Vector2(1,0);footer.pivot=new Vector2(.5f,0);footer.sizeDelta=new Vector2(0,280);footer.anchoredPosition=Vector2.zero;
        selection=UXUI.Label(footer,"Tap one of your pieces to see its moves.",4,108,23);
        left=UXUI.Button(footer,"",112,()=>PreviewRotation(false));Half(left,false);
        right=UXUI.Button(footer,"",112,()=>PreviewRotation(true));Half(right,true);
        lastAction=UXUI.Label(footer,"",112,62,23);
        var zoomButton=UXUI.Button(footer,"Zoom / reset",198,ToggleZoom);Half(zoomButton,false);
        var deselect=UXUI.Button(footer,"Deselect",198,Deselect);Half(deselect,true);
        var background = new GameObject("Board backdrop",typeof(Camera));background.transform.SetParent(transform,false);
        var backdrop=background.GetComponent<Camera>();backdrop.depth=camera.depth-1;backdrop.cullingMask=0;
        backdrop.clearFlags=CameraClearFlags.SolidColor;backdrop.backgroundColor=UXUI.Ink;
        camera.clearFlags=CameraClearFlags.SolidColor;
        var shader=Shader.Find("Sprites/Default");lineMaterial=new Material(shader);
        selectedLine=Circle("Selected piece",new Color(.95f,.8f,.4f),.012f);
        fromLine=Circle("Last move origin",new Color(.8f,.7f,.4f,.7f),.008f);
        toLine=Circle("Last move destination",new Color(.3f,.9f,.7f),.012f);
        ringLine=Circle("Rotation preview",new Color(.9f,.75f,.4f),.018f);
        selectedLine.gameObject.SetActive(false);fromLine.gameObject.SetActive(false);toLine.gameObject.SetActive(false);ringLine.gameObject.SetActive(false);
        wasOnline=NetworkController.IsOnline;
        if(Learning)gameObject.AddComponent<PlayableLesson>();
        // The legacy overlay is superseded by the persistent status/header here.
        foreach(var overlay in FindObjectsByType<OnlineStatusOverlay>(FindObjectsInactive.Exclude))overlay.gameObject.SetActive(false);
        if(AnalysisUX.Pending!=null && !Learning)StartCoroutine(OpenPendingAnalysis());
        else if(PuzzleUX.Requested && !Learning)PuzzleUX.Install(this);
    }
    IEnumerator OpenPendingAnalysis()
    {
        yield return null; // let game.Start place the pieces first
        var e=AnalysisUX.Pending;AnalysisUX.Pending=null;
        resultShown=true;
        AnalysisUX.Open(e,true);
    }
    void Half(Button button,bool rightSide)
    {
        var r=(RectTransform)button.transform;r.anchorMin=new Vector2(rightSide?.5f:0,1);r.anchorMax=new Vector2(rightSide?1:.5f,1);
        r.offsetMin=new Vector2(12,r.offsetMin.y);r.offsetMax=new Vector2(-12,r.offsetMax.y);
    }
    void Update()
    {
        if(Board==null||safe==null)return;
        LayoutBoard();
        if(Board.IsGameOver() && !Learning && !resultShown && !PuzzleUX.Active){resultShown=true;ShowResult();}
        bool canRotatePiece=Selected!=null && (Selected.pieceType=="S"||Selected.pieceType=="W");
        bool rotation=canRotatePiece && !Board.RotationsBlocked;
        left.gameObject.SetActive(rotation);right.gameObject.SetActive(rotation);lastAction.gameObject.SetActive(!rotation);
        if(rotation)
        {
            string pair=PairName(Selected.ringNumber);
            left.GetComponentInChildren<Text>().text="↶ "+pair+" · 90°";right.GetComponentInChildren<Text>().text="↷ "+pair+" · 90°";
        }
        if(Selected!=null){Draw(selectedLine,Selected.transform.position,.09f);selection.text=Ability(Selected);
            if(canRotatePiece && Board.RotationsBlocked)selection.text+="\n"+game.MaxConsecutiveRotations+" rotations in a row: move a piece this turn.";}
        else {selectedLine.gameObject.SetActive(false);selection.text="Tap a piece · Mint = move · Coral = capture";}
        if(Time.unscaledTime<notificationUntil)selection.text=notification;
        string current=Board.GetCurrentPlayer();
        turn.text=Learning?"Practice · Orange to move":Board.IsGameOver()?"Game complete":connectionLost?"Connection interrupted":NetworkController.IsOnline?
            (!NetworkController.MatchReady?"Waiting for opponent…":current==NetworkController.LocalColor?"Your turn · "+Team(current):"Opponent's turn · "+Team(current)):
            ai!=null && current==ai.PlayerColor?"AI is thinking…":ai!=null?"Your turn · "+Team(current):Team(current)+" to move";
        progress.text=Summary("orange")+"\n"+Summary("green");lastAction.text=lastDescription;
        if(wasOnline && !Board.IsGameOver())
        {
            if((!NetworkController.IsOnline || !NetworkController.MatchReady) && NetworkController.StatusMessage!=observedStatus &&
                (NetworkController.SessionInterrupted || !NetworkController.IsOnline))
            {observedStatus=NetworkController.StatusMessage;connectionLost=true;ConnectionDialog();}
        }
        if(NetworkController.IsOnline && !NetworkController.MatchReady && !connectionLost && modal==null) WaitingDialog();
        if(Input.GetKeyDown(KeyCode.Escape) && !connectionLost && !Board.IsGameOver() && !Reviewing){if(modal==null)ShowMenu();else CloseModal();}
        Gestures();
    }
    void LayoutBoard()
    {
        float scale=Screen.height/1280f;Rect a=Screen.safeArea;
        float bottom=a.yMin+290*scale,top=a.yMax-245*scale;
        camera.rect=new Rect(a.xMin/Screen.width,bottom/Screen.height,a.width/Screen.width,Mathf.Max(1,top-bottom)/Screen.height);
        float aspect=a.width/Mathf.Max(1,top-bottom);fitSize=Mathf.Max(2.2f,2.2f/aspect);
        camera.orthographicSize=fitSize/zoom;
        ClampCamera();
    }
    void ClampCamera()
    {
        var p=camera.transform.position;float x=Mathf.Max(0,2.1f-camera.orthographicSize*camera.aspect),y=Mathf.Max(0,2.1f-camera.orthographicSize);
        p.x=Mathf.Clamp(p.x,-x,x);p.y=Mathf.Clamp(p.y,-y,y);camera.transform.position=p;
    }
    public void ToggleZoom(){zoom=zoom>1?1:2;Notify(zoom>1?"Drag to pan · Pinch to zoom · Zoom / reset to fit":"Board fitted to screen");}
    public void Notify(string message){notification=message;notificationUntil=Time.unscaledTime+4;}
    void Gestures()
    {
        if(modal!=null)return;
        if(Input.touchCount==2)
        {
            var a=Input.GetTouch(0);var b=Input.GetTouch(1);
            if(!camera.pixelRect.Contains(a.position)||!camera.pixelRect.Contains(b.position))return;
            gesture=true;
            float before=((a.position-a.deltaPosition)-(b.position-b.deltaPosition)).magnitude;
            if(before>1)zoom=Mathf.Clamp(zoom*Vector2.Distance(a.position,b.position)/before,1,3);
            Vector2 delta=(a.deltaPosition+b.deltaPosition)*.5f;
            camera.transform.position-=(Vector3)(delta*camera.orthographicSize*2/camera.pixelHeight);return;
        }
        if(Input.touchCount==1)
        {
            var t=Input.GetTouch(0);
            if(t.phase==TouchPhase.Began){lastPan=t.position;gesture=false;}
            if(!camera.pixelRect.Contains(lastPan))return;
            if(zoom>1 && t.phase==TouchPhase.Moved && camera.pixelRect.Contains(t.position) && Vector2.Distance(lastPan,t.position)>18)
            {gesture=true;camera.transform.position-=(Vector3)(t.deltaPosition*camera.orthographicSize*2/camera.pixelHeight);}
        }
        if(Input.touchCount==0 && Input.mouseScrollDelta.y!=0 && camera.pixelRect.Contains(Input.mousePosition))
            zoom=Mathf.Clamp(zoom+Input.mouseScrollDelta.y*.15f,1,3);
    }
    public bool ConsumeGesture => gesture;
    public bool InBoard(Vector2 screen) => camera!=null && camera.pixelRect.Contains(screen);
    public void Select(Chessman piece)
    {
        if(Selected==piece){Deselect();return;}Selected=piece;PieceSelected?.Invoke(piece);
    }
    public void Deselect()
    {
        Selected=null;
        foreach(var plate in FindObjectsByType<MovePlate>(FindObjectsInactive.Exclude)){plate.gameObject.SetActive(false);Destroy(plate.gameObject);}
        if(selectedLine!=null)selectedLine.gameObject.SetActive(false);
    }
    public void Record(game.MoveInfo move)
    {
        Deselect();Draw(fromLine,Chessman.PositionAt(move.fromRing,move.fromPlace),.075f);Draw(toLine,Chessman.PositionAt(move.toRing,move.toPlace),.085f);
        lastDescription=Team(move.player)+": "+PieceName(move.pieceType)+" · "+Chessman.RING_NAMES[move.fromRing-1]+move.fromPlace+" → "+Chessman.RING_NAMES[move.toRing-1]+move.toPlace;
        MoveCompleted?.Invoke();
    }
    public void Rotated(int pair,bool clockwise)
    {
        Deselect();fromLine.gameObject.SetActive(false);toLine.gameObject.SetActive(false);
        lastDescription="Rotated "+new[]{"A/a","B/b","C/c","D"}[pair-1]+(clockwise?" clockwise":" counterclockwise");RotationCompleted?.Invoke();
    }
    public void AnimatePiece(Transform piece,Vector3 target,float arc=0)
    {
        if(tweens.ContainsKey(piece)){StopCoroutine(tweens[piece]);tweens.Remove(piece);animations=Mathf.Max(0,animations-1);}
        tweens[piece]=StartCoroutine(Tween(piece,target,arc));
    }
    IEnumerator Tween(Transform piece,Vector3 end,float arc)
    {
        animations++;Vector3 start=piece.position;
        float duration=arc==0?.24f:.4f;
        for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
        {
            if(piece==null)break;
            float f=Mathf.SmoothStep(0,1,t/duration);
            piece.position=arc==0?Vector3.Lerp(start,end,f):Quaternion.Euler(0,0,arc*f)*new Vector3(start.x,start.y,0)+new Vector3(0,0,end.z);
            yield return null;
        }
        if(piece!=null)piece.position=end;
        animations--;tweens.Remove(piece);
    }
    LineRenderer Circle(string name,Color color,float width)
    {
        var go=new GameObject(name);go.transform.SetParent(transform);var line=go.AddComponent<LineRenderer>();
        line.sharedMaterial=lineMaterial;line.startColor=line.endColor=color;line.startWidth=line.endWidth=width;
        line.loop=true;line.positionCount=96;line.sortingOrder=20;return line;
    }
    void Draw(LineRenderer line,Vector3 center,float radius)
    {
        if(line==null)return;line.gameObject.SetActive(true);center.z=-4;
        for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96;line.SetPosition(i,center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius);}
    }
    public void PreviewRotation(bool clockwise)
    {
        if(BlockInput||Selected==null||(Selected.pieceType!="S"&&Selected.pieceType!="W"))return;
        if(Board.RotationsBlocked){Notify(game.MaxConsecutiveRotations+" rotations in a row: move a piece this turn.");return;}
        var piece=Selected;int pair=(piece.ringNumber+1)/2;
        float radius=new[]{2f,1.5f,1f,.5f}[pair-1];Draw(ringLine,Vector3.zero,radius);
        var p=OpenModal("Rotate "+PairName(piece.ringNumber)+"?");
        UXUI.Label(p,(clockwise?"↷ Clockwise":"↶ Counterclockwise")+" · 90°\n\nAll pieces on the highlighted ring pair travel with it. Rotation uses your turn.",130,210,28);
        // Ghost the actual destination of every affected piece without changing the board.
        var ghosts=new GameObject("Rotation destinations");ghosts.transform.SetParent(transform);
        foreach(var cm in FindObjectsByType<Chessman>(FindObjectsInactive.Exclude))
            if((cm.ringNumber+1)/2==pair)
            {
                var ghost=new GameObject("Preview "+cm.name);ghost.transform.SetParent(ghosts.transform);
                ghost.transform.position=Quaternion.Euler(0,0,clockwise?-90:90)*cm.transform.position;
                ghost.transform.localScale=cm.transform.lossyScale;var r=ghost.AddComponent<SpriteRenderer>();var original=cm.GetComponent<SpriteRenderer>();
                r.sprite=original.sprite;r.color=new Color(.5f,1,.8f,.6f);r.sortingOrder=15;
            }
        // This preview panel leaves the board visible. It occupies only the top header.
        UXUI.Place(p,0,240,0);p.GetComponent<Image>().color=UXUI.Surface;
        foreach(var label in p.GetComponentsInChildren<Text>())label.gameObject.SetActive(false);
        UXUI.Label(p,(clockwise?"↷ ":"↶ ")+PairName(piece.ringNumber)+" · Preview of 90° rotation",8,70,27);
        var confirm=UXUI.Button(p,"Rotate",100,()=>{Destroy(ghosts);CloseModal();Board.RotateRing(pair,clockwise);},true);Half(confirm,false);
        var cancel=UXUI.Button(p,"Cancel",100,()=>{Destroy(ghosts);CloseModal();});Half(cancel,true);
        previewGhosts=ghosts;
    }
    GameObject previewGhosts;
    RectTransform OpenModal(string title){CloseModal();modal=UXUI.Modal(safe,title);return modal;}
    public void CloseModal(){if(modal!=null){modal.gameObject.SetActive(false);Destroy(modal.gameObject);}modal=null;if(ringLine!=null)ringLine.gameObject.SetActive(false);if(previewGhosts!=null)Destroy(previewGhosts);}
    public void ShowObjectives()
    {
        var p=OpenModal("Winning combinations");
        UXUI.Label(p,Details("orange")+"\n\n"+Details("green"),110,670,26);
        UXUI.Button(p,"Rules",830,()=>UXUI.Rules(p,null));UXUI.Button(p,"Back to game",920,CloseModal);
    }
    void ShowResult(){ShowResultScreen();}
    public void ShowResultScreen()
    {
        if(Board.VictoryPanel!=null)Board.VictoryPanel.SetActive(false);
        var p=OpenModal(Board.GetWinner()=="draw"?"Draw":Team(Board.GetWinner())+" wins!");
        UXUI.Label(p,Board.ResultExplanation,150,160,31);
        var record=Board.LastRecordedGame;
        // An opponent who left already took IsOnline down, so ask the saved game what kind it was.
        bool online=record!=null?record.mode=="online":NetworkController.IsOnline;
        if(online)UXUI.Label(p,NetworkController.LastResultMessage,340,130,26);
        if(record!=null)UXUI.Button(p,"Analyse game",500,()=>AnalysisUX.Open(record,false),true);
        if(!online)UXUI.Button(p,"Play again",590,()=>SceneManager.LoadScene(gameObject.scene.name),record==null);
        UXUI.Button(p,"Return to menu",680,Leave);
    }
    public void ShowMenu()
    {
        var p=OpenModal("Game menu");
        UXUI.Label(p,NetworkController.IsOnline?"Online play continues while this menu is open.":"Game paused",110,75,26);
        UXUI.Button(p,"Resume",240,CloseModal,true);
        UXUI.Button(p,"Sound: "+(AudioListener.volume>0?"On":"Off"),330,()=>{UXSettings.ToggleSound();ShowMenu();});
        UXUI.Button(p,"Rules",420,()=>UXUI.Rules(p,null));
        UXUI.Button(p,"Settings",510,()=>SettingsUX.Show(p));
        UXUI.Button(p,"Leave game",600,ConfirmLeave);
    }
    void ConfirmLeave()
    {
        var p=OpenModal("Leave this game?");
        UXUI.Label(p,WillForfeit()?"Leaving now counts as a loss"+(NetworkController.IsOnline?" ("+(-game.ForfeitPoints)+" points).":"."):"This match will end on this device. Your position will not be saved.",150,170,28);
        UXUI.Button(p,"Keep playing",420,CloseModal,true);UXUI.Button(p,"Leave game",520,Leave);
    }
    // Would leaving right now be a forfeit? (A move has been made in a game against the AI or online.)
    bool WillForfeit()
    {
        if(Board==null||Board.IsGameOver()||Learning||PuzzleUX.Active||Board.MoveCodes.Count==0)return false;
        if(NetworkController.IsOnline)return NetworkController.MatchReady;
        var ai=FindAnyObjectByType<AIAgent>();return ai!=null&&!ai.aiVsAiTest;
    }
    // Every way out of a running game goes through here: walking away from a game in progress is a loss.
    public void Leave(){if(Board!=null)Board.Forfeit(true);NetworkController.GoOffline();SceneManager.LoadScene("MainMenu");}
    void ConnectionDialog()
    {
        var p=OpenModal("Match interrupted");UXUI.Label(p,NetworkController.StatusMessage+"\n\nThis match cannot continue. Return to the menu to start a new game.",140,220,28);
        UXUI.Button(p,"Return to menu",470,Leave,true);
    }
    void WaitingDialog()
    {
        var p=OpenModal("Waiting for an opponent");
        UXUI.Label(p,"Room code: "+NetworkController.RoomCode+"\n\nSend this code to a friend. They can enter it under Play online → Join room.",140,210,28);
        copy=UXUI.Button(p,"Copy invitation",410,()=>{GUIUtility.systemCopyBuffer="Join me in Manu et Mente! Room code: "+NetworkController.RoomCode;copy.GetComponentInChildren<Text>().text="Invitation copied";});
        UXUI.Button(p,"Copy room code",500,()=>{GUIUtility.systemCopyBuffer=NetworkController.RoomCode;copy.GetComponentInChildren<Text>().text="Room code copied";});
        UXUI.Button(p,"Cancel and return",610,Leave);
        StartCoroutine(WaitForOpponent(p));
    }
    IEnumerator WaitForOpponent(RectTransform p){while(p!=null&&!NetworkController.MatchReady&&!connectionLost)yield return null;if(p!=null&&NetworkController.MatchReady)CloseModal();}
    string Summary(string team)
    {
        var c=Count(team);return Team(team)+" · D: Hand/Mind "+c[0]+"/2, Pawns "+c[1]+" · C/c: "+c[2]+" Pawns";
    }
    public string Details(string team)
    {
        var c=Count(team);int enemy=Count(team=="orange"?"green":"orange")[3];
        return Team(team)+"\nHand + Mind in D: "+c[0]+" / 2\nCommander + 3 Pawns in D: "+Mathf.Min(c[0],1)+" / 1 + "+c[1]+" / 3\nAfter losing a commander - last commander in D + 4 Pawns in C/c: "+(Board.CountCommanders(team)==1?Mathf.Min(c[0],1)+" / 1 + "+c[2]+" / 4":"not available")+"\nEnemy commanders captured: "+(2-enemy)+" / 2";
    }
    int[] Count(string team)
    {
        int[] c=new int[4];for(int r=1;r<=7;r++)for(int p=1;p<=Chessman.RING_SIZES[r-1];p++)
        {var obj=Board.GetPosition(r,p);if(obj==null)continue;var cm=obj.GetComponent<Chessman>();if(cm.player!=team)continue;
            if(cm.pieceType=="H"||cm.pieceType=="B"){c[3]++;if(r==7)c[0]++;}
            if(cm.pieceType=="P"){if(r==7)c[1]++;if(r==5||r==6)c[2]++;}}
        return c;
    }
    public static string Team(string team)=>team=="orange"?"Orange":"Green";
    static string PairName(int ring)=>new[]{"A/a","B/b","C/c","D"}[(ring-1)/2];
    public static string PieceName(string type)
    {switch(type){case "H":return "Hand";case "B":return "Mind";case "P":return "Pawn";case "T":return "Technician";case "A":return "Apprentice";case "S":return "Scholar";default:return "Sage";}}
    static string Ability(Chessman c)
    {
        string rule;
        switch(c.pieceType){case "H":case "B":rule="Steps around a ring or radially. Cannot capture.";break;
        case "T":rule="Steps around a ring or radially. Can capture; enables Apprentices.";break;
        case "A":rule="Diagonal moves and captures. A Technician unlocks deeper ring pairs.";break;
        case "S":rule="Diagonal moves; a Sage unlocks deeper rings. Rotate instead of moving. En passant captures only.";break;
        case "W":rule="Steps around a ring or radially. Enables Scholars; can rotate. En passant captures only.";break;
        default:rule="Steps to nearby cells. Captures through the game's en passant rule.";break;}
        return PieceName(c.pieceType)+" · "+Team(c.player)+"\n"+rule;
    }
    void OnDestroy(){if(Instance==this)Instance=null;if(lineMaterial!=null)Destroy(lineMaterial);}
}
