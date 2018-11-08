﻿
using JPT_TosaTest.Classes;
using JPT_TosaTest.Config.SoftwareManager;
using JPT_TosaTest.Vision;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JPT_TosaTest.WorkFlow
{
    public enum STEP : int
    {
        Init,
        CmdGetProduct,
        CmdFindLine,
        CmdWorkTop,
        CmdWorkBottom,
        DO_NOTHING,
       
        EXIT,
    }
    public class WorkService : WorkFlowBase
    {
        #region Private
        //start from 0
        private const int AXIS_X = 3, AXIS_Y1 = 1, AXIS_Y2 = 2, AXIS_Z = 0, AXIS_CY = 5, AXIS_CZ = 4, AXIS_R = 6;
        private const int PT_X = 0, PT_Y1 = 1, PT_Y2 = 2, PT_Z = 3, PT_R = 4, PT_CY = 5, PT_CZ = 6;
        private const int VAC_PLC = 0,VAC_HSG=2;
        private MotionCards.IMotion  MotionCard=null;
        private IOCards.IIO IOCard = null;
        private int ProductIndex = 0;
        private object Hom_2D=null, ModelPos=null;
        private List<object> TopLines = null;
        private List<object> BottomLines = null;
        private string File_PairToolParaPath = $"{FileHelper.GetCurFilePathString()}VisionData\\ToolData\\PairToolData\\";
        private string File_ModelFileName = $"VisionData\\Model\\Cam0_Model.shm";    //Model
        private string File_LineToolParaPath = $"{FileHelper.GetCurFilePathString()}VisionData\\ToolData\\LineToolData\\";

        private List<double> PadCamTopPos = null;
        List<double> PadCamBottomPos = null;
        List<double> LensCamTopPos = null;
        List<double> LensCamBottomPos = null;
        List<double> PtLeftTop = null;
        List<double> PtRightDown = null;
        List<double> PtDropDown = null;
        #endregion

        protected override bool UserInit()
        {
            MotionCard = MotionCards.MotionMgr.Instance.FindMotionCardByCardName("Motion_IrixiEE0017[0]");
            IOCard = IOCards.IOCardMgr.Instance.FindIOCardByCardName("IO_IrixiEE0017[0]");
           
            return GetAllPoint() && MotionCard !=null  &&  IOCard!=null;
        }
        public WorkService(WorkFlowConfig cfg) : base(cfg)
        {

        }
        protected override int WorkFlow()
        {
            ClearAllStep();
            PushStep(STEP.Init);
            while (!cts.IsCancellationRequested)
            {
                Thread.Sleep(10);
                Step = PeekStep();
                if (bPause || Step==null)
                    continue;             
             
                switch (Step)
                {
                    case STEP.Init:     //初始化
                        ShowInfo();
                        HomeAll();
                        ClearAllStep();
                        break;
                    case STEP.CmdGetProduct:    //取产品
                        GetProduct(++ProductIndex);
                        if (ProductIndex >= 6)
                            ProductIndex = 0;
                        ClearAllStep();
                        break;
                    case STEP.CmdFindLine:
                        FindAndGetModelData();
                        ClearAllStep();
                        break;
                    case STEP.CmdWorkTop:   //找上表面的线
                        WorkTop();
                        ClearAllStep(); 
                        break;
                    case STEP.CmdWorkBottom:  //找下表面的线
                        WorkBottom();
                        ClearAllStep();
                        break;

                    case STEP.EXIT:
                        return 0;
                }
            }
            return 0;
        }

        #region Private
        private void HomeAll()
        {
            int nStep = 0;
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {
                    case 0:
                        MotionCard.Home(AXIS_CZ, 0, 500, 5, 10);
                        MotionCard.Home(AXIS_Z, 0, 500, 20, 50);
                        nStep = 1;
                        break;
                    case 1:
                        if (MotionCard.IsHomeStop(AXIS_CZ) && MotionCard.IsHomeStop(AXIS_Z))
                        {
                            nStep = 2;
                        }
                        break;
                    case 2:
                        MotionCard.Home(AXIS_X, 0, 500, 20, 50);
                        MotionCard.Home(AXIS_Y2, 0, 500, 20, 50);
                        MotionCard.Home(AXIS_Y1, 0, 500, 20, 50);
                        MotionCard.Home(AXIS_CY, 0, 500, 5, 10);
                        nStep = 3;
                        break;
                    case 3:
                        if (MotionCard.IsHomeStop(AXIS_X) && MotionCard.IsHomeStop(AXIS_Y1) && MotionCard.IsHomeStop(AXIS_Y2) && MotionCard.IsHomeStop(AXIS_CY))
                        {
                            nStep = 4;
                        }
                        break;
                    case 4:
                        ShowInfo("HomeOk");
                        return;
                    default:
                        return;

                }
            }
        }
        private void GetProduct(int Index)
        {
            if (!GetAllPoint())
            {
                ShowError("获取点位出现错误");
                return;
            }
            if (Index < 1 || Index > 6)
                return;

            //2行3列
            double DeltaX = (PtRightDown[PT_X] - PtLeftTop[PT_X]) / 2;

            double TargetX = 0;
            double TargetY1 = 0;
            if (Index >= 1 && Index <= 3)
            {
                TargetX= PtLeftTop[PT_X] + DeltaX * (Index - 1);
                TargetY1 =  PtLeftTop[PT_Y1] ;
            }
            else
            {
                TargetX = PtLeftTop[PT_X] + DeltaX * (Index - 4);
                TargetY1 = PtRightDown[PT_Y1];
            }
            
            int nStep = 0;
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {
                    case 0: //先将Z升起防止干涉
                        MotionCard.MoveAbs(AXIS_Z, 500, 100, 0);
                        nStep = 1;
                        break;
                    case 1: //移动PT_X到取料点
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            MotionCard.MoveAbs(AXIS_X, 500, 100, TargetX);
                            nStep = 2;
                        }
                        break;
                    case 2: // PT_Y1到取料点
                        if (MotionCard.IsNormalStop(AXIS_X))
                        {
                            MotionCard.MoveAbs(AXIS_Y1, 500, 100, TargetY1);
                            nStep = 3;
                        }
                        break;
                    case 3:
                        if (MotionCard.IsNormalStop(AXIS_Y1))
                        {
                            nStep = 4;
                        }
                        break;
                    case 4: //下降PT_Z轴
                        MotionCard.MoveAbs(AXIS_Z, 500, 100, PtLeftTop[PT_Z]);
                        nStep = 5;
                        break;
                    case 5:
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {

                            nStep = 6;
                        }
                        break;
                    case 6: //吸真空
                        IOCard.WriteIoOutBit(VAC_PLC, true);
                        nStep = 7;
                        break;    
                     case 7: //PT_Z轴抬起
                        Thread.Sleep(500);
                        MotionCard.MoveAbs(AXIS_Z,500,100,0);
                        nStep = 8;
                        break;
                    case 8:
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            MotionCard.MoveAbs(AXIS_X,500,100, PtDropDown[PT_X]);
                            nStep = 9;
                        }
                        break;

                    case 9: //移动PT_Y2过去
                        if (MotionCard.IsNormalStop(AXIS_X))
                        {
                            MotionCard.MoveAbs(AXIS_Y2, 500, 100, PtDropDown[PT_Y2]);
                            nStep = 10;
                        }
                        break;

                    case 10: //移动到放置点的PT_X位置并下降PT_Z到HSG表面，不要下去
                        if (MotionCard.IsNormalStop(AXIS_Y2))
                        {
                            MotionCard.MoveAbs(AXIS_Z, 500, 100, PtDropDown[PT_Z]);
                            nStep = 11;
                        }
                        break;
                    case 11: //
                        if (MotionCard.IsNormalStop(AXIS_Z))
                        {
                            nStep = 12;
                        }
                        break;
                    case 12:    //等待调整位置
                        ShowInfo("取料完毕");
                        return;
                    default:
                        return;
                }
            }

        }

        /// <summary>
        /// 找模板
        /// </summary>
        private void FindAndGetModelData()
        {
            int nStep = 0;
            ShowInfo("正在标记......");
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {
                    case 0: //移动CY
                        MotionCard.MoveAbs(AXIS_CY, 1000, 10, PadCamBottomPos[PT_CY]);
                        nStep = 1;
                        break;
                    case 1: //CZ到下表面
                        if (MotionCard.IsNormalStop(AXIS_CY))
                        {
                            MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PadCamBottomPos[PT_CZ]);                            nStep = 2;
                           
                        }
                        break;
                   
                    case 2: //开始寻找模板
                        if (MotionCard.IsNormalStop(AXIS_CZ))
                        {
                            Thread.Sleep(200);
                            HalconVision.Instance.GrabImage(0);
                            Vision.HalconVision.Instance.ProcessImage(HalconVision.IMAGEPROCESS_STEP.T1, 0, File_ModelFileName, out object Hom2DAndModelPos);
                            if (Hom2DAndModelPos != null)
                            {
                                List<object> list = Hom2DAndModelPos as List<object>;
                                if (list.Count == 2)
                                {
                                    Hom_2D = list[0];
                                    ModelPos = list[1];
                                }
                            }
                            nStep = 3;
                        }
                        break;
                    case 3: //模板找到以后开始找下表面线
                        {
                            FindLineBottom(out BottomLines);
                            MotionCard.MoveAbs(AXIS_CZ, 1000, 10, LensCamTopPos[PT_CZ]);       
                            nStep = 4;   
                        }
                        break;
                    case 4:
                        if (MotionCard.IsNormalStop(AXIS_CZ))
                        {
                            {
                                MotionCard.MoveAbs(AXIS_CY, 1000, 10, LensCamTopPos[PT_CY]);
                                nStep = 5;
                            }
                        }
                        break;
             
                    case 5: //升到上表面寻找上表面的线
                        if (MotionCard.IsNormalStop(AXIS_CY))
                        {
                            {
                                Thread.Sleep(200);
                                HalconVision.Instance.GrabImage(0);
                               
                                FindLineTop(out TopLines);
                                //顺便将图像尺寸标定了
                                HalconVision.Instance.ProcessImage(HalconVision.IMAGEPROCESS_STEP.T5, 0, TopLines, out object result);
                                nStep = 6;
                            }
                        }
                        break;
                    case 6: // 完毕,等待工作
                        ShowInfo("标记完毕");
                        return;
                    default:
                        return;
                }
            }
            ShowInfo("标记被终止");
        }

        //顶部监视
        private void WorkTop()
        {
            ShowInfo("Lens......");
            int nStep = 0;
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {        
                    case 0: //CZ到上表面    
                        MotionCard.MoveAbs(AXIS_CZ, 1000, 10, LensCamTopPos[PT_CZ]);
                        nStep = 1;
                        break;
                    case 1: //移动CY
                    if (MotionCard.IsNormalStop(AXIS_CZ))
                        {
                            MotionCard.MoveAbs(AXIS_CY, 1000, 10, LensCamTopPos[PT_CY]);
                            nStep = 2;
                        }
                        break;
                    case 2: 
                        if (MotionCard.IsNormalStop(AXIS_CY))
                        {
                            Thread.Sleep(200);
                            nStep = 3;
                        }
                        break;
                    case 3: //开始拍照并显示
                        HalconVision.Instance.GrabImage(0,true,true);
                        HalconVision.Instance.ProcessImage(HalconVision.IMAGEPROCESS_STEP.T3, 0, TopLines, out object r);
                        if(GetCurStepCount()==0)
                            nStep = 4;
                        break;
                    case 4: // 完毕,等待工作完毕
                        ShowInfo("Lens完毕");
                        return;
                    default:
                        return;
                }
            }
            ShowInfo("Lens被终止");
        }

        //底部
        private void WorkBottom()
        {
            int nStep = 0;
            ShowInfo("Pad......");
            while (!cts.IsCancellationRequested)
            {
                switch (nStep)
                {
                    case 0: //移动CY
                        MotionCard.MoveAbs(AXIS_CY, 1000, 10, PadCamBottomPos[PT_CY]);
                        nStep = 1;
                        break;
                    case 1: //CZ到下表面
                        if (MotionCard.IsNormalStop(AXIS_CY))
                        {
                            MotionCard.MoveAbs(AXIS_CZ, 1000, 10, PadCamBottomPos[PT_CZ]);
                            
                            nStep = 2;
                        }
                        break;
                    case 2: //开始拍照
                        if (MotionCard.IsNormalStop(AXIS_CZ))
                        {
                            Thread.Sleep(200);//等待稳定
                            nStep = 3;
                        }
                        break;
                    case 3:
                        HalconVision.Instance.GrabImage(0, true, true);
                        HalconVision.Instance.ProcessImage(HalconVision.IMAGEPROCESS_STEP.T4, 0, BottomLines, out object r);
                        if (GetCurStepCount() == 0)
                            nStep = 4;
                        break;
                    case 4: // 完毕,等待工作完毕
                        ShowInfo("Pad完毕");
                        return;
                    default:
                        return;
                }
            }
            ShowInfo("Pad被终止");
        }
        #endregion



        private bool FindLineTop(out List<object> lineList)
        {
            lineList = new List<object>();
            try
            {
                List<string> listParas = new List<string>();
                var fileList = FileHelper.GetProfileList(File_LineToolParaPath);
                foreach (var file in fileList)
                {
                    if (file.Contains("Top"))
                    {
                        listParas.Add(File.ReadAllText($"{File_LineToolParaPath}{file}.para"));
                    }
                }
                if (Hom_2D != null && ModelPos != null)
                {
                    HalconVision.Instance.ProcessImage(HalconVision.IMAGEPROCESS_STEP.T2, 0, new List<object> { Hom_2D, ModelPos, listParas }, out object result);
                    lineList = result as List<object>;
                }
                else
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
        private void BackToInitPos()
        {

        }
        private bool FindLineBottom(out List<object> lineList)
        {
            lineList = null;
            try
            {
                List<string> listParas = new List<string>();  
                var fileList = FileHelper.GetProfileList(File_PairToolParaPath);
                foreach (var file in fileList)
                {
                    if (file.Contains("Bottom"))
                    {
                        listParas.Add(File.ReadAllText($"{File_PairToolParaPath}{file}.para"));
                    }
                }
                if (Hom_2D != null && ModelPos != null)
                {
                    HalconVision.Instance.ProcessImage(HalconVision.IMAGEPROCESS_STEP.T2, 0, new List<object> { Hom_2D, ModelPos, listParas }, out object result);
                    lineList = result as List<object>;
                }
                else
                {
                    //TO DO
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        private bool GetAllPoint()
        {
            PadCamTopPos = WorkFlowMgr.Instance.GetPoint("Pad相机顶部位置");
            PadCamBottomPos = WorkFlowMgr.Instance.GetPoint("Pad相机底部位置");
            LensCamTopPos = WorkFlowMgr.Instance.GetPoint("Lens相机顶部位置");
            LensCamBottomPos = WorkFlowMgr.Instance.GetPoint("Lens相机底部位置");
            PtLeftTop = WorkFlowMgr.Instance.GetPoint("左上吸取点");
            PtRightDown = WorkFlowMgr.Instance.GetPoint("右下吸取点");
            PtDropDown = WorkFlowMgr.Instance.GetPoint("放置点");

            return  PadCamTopPos != null &&
                    PadCamBottomPos != null &&
                    LensCamTopPos != null &&
                    LensCamBottomPos != null &&
                    PtLeftTop != null &&
                    PtRightDown != null &&
                    PtDropDown != null;
        }

    }
}
