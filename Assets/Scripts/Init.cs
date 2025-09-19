using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Init : MonoBehaviour
{
    public static Init Instance;
    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            if (args.Exception.InnerException != null)
            {
                Debug.LogError(args.Exception.InnerException);
            }
            else
                Debug.LogError(args.Exception);
        };
    }

    private async void Start()
    {
        await UnityEngine.AddressableAssets.Addressables.InitializeAsync().Task;
        await Database.Instance.Init();
        //ModifyManager.Instance.Init();
        GameData.Instance.Init();
        AudioManager.Instance.PlayBackgroundAudio("main");
        //Debug.Log(Database.Instance.Get<UnitData>(0).Id);
        //SpineImportHelper.Instance.Init();
        //SpineImportHelper.Instance.LoadSpineAssets(
        //    "丰川祥子",
        //    "D:\\UnityWork\\zhou-master\\Assets\\StreamingAssets\\Spine\\丰川祥子\\back\\oblvns.png",
        //    "D:\\UnityWork\\zhou-master\\Assets\\StreamingAssets\\Spine\\丰川祥子\\back\\oblvns.atlas.txt",
        //    "D:\\UnityWork\\zhou-master\\Assets\\StreamingAssets\\Spine\\丰川祥子\\back\\oblvns.skel.bytes"
        //    );
        var battleUI = UIManager.Instance.ChangeView<MainUI.UI_Main>(MainUI.UI_Main.URL);
        //OpenFilePanel();
        //StartBattle(new BattleInput()
        //{
        //    MapName = "TestMap",
        //    UnitInputs = new UnitInput[]
        //    {
        //        new UnitInput(){ Id=2 },
        //        new UnitInput(){ Id=3 },
        //        new UnitInput(){ Id=3 },
        //        new UnitInput(){ Id=4 },
        //    },
        //    StartCost=50,
        //});
    }
}
