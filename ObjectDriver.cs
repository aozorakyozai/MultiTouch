using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
//using UnityEngine.UI;


/************************************************************
 * スマホ画面操作 【 ObjectDriver 】
 * 環境：Unity 2023.3.13f1 LTS
 * 環境：Unity C# 
 * 操作内容：Tap,DoubleTap,Hold,Swipe,Flick,PinchOut,PinchIn,3Tap,4Tap
 * ３Dオブジェクトをワールド座標で回転させる
 * cubeLength:1辺のCube個数,cubeGap:隙間の大きさを入力すること
 * タップした場所のオブジェクトの色を変える
 * 注意：複数タップ状態からシングルタップ、スワイプ、フリックに継続しない仕様
 * 問題：フリックがバラバラ、長押しが長すぎる
*************************************************************/

public class ObjectDriver : MonoBehaviour
{
    /**** 左下テキスト ****/
    [SerializeField] TextMeshProUGUI textHead;
    [SerializeField] TextMeshProUGUI textBottom;

    /**** Cube構造設定 ****/
    // 中央のキューブの指定
    [SerializeField] string centerObject = "CenterCube";
    // transformに毎回アクセスすると重くなるから、キャッシュするため
    private Transform _transform;
    // １辺のCube数
    [SerializeField] int cubeLength;
    // cubeNameと座標の調整数
    float adjusrNum { get { return (cubeLength + 1) / 2f; } }
    // デフォルト位置のキューブ間の距離 (1mのCubeの場合：1.1は,1m + 0.1mの隙間がある)
    [SerializeField] float cubeGap = 1.1f;
    // Childキューブを広げた時のキューブ間の距離
    float pinchLength { get { return cubeGap * 2f; } }
    // Childキューブ用 ParentCubeに対して x+2,y+2,z+2
    string cubeNumber;

    /**** MultiTouch座標設定 ****/
    // 始点の座標
    Vector2 startTapPosition;
    // 終点の座標
    Vector2 endPressPosition;
    // スワイプの起点の座標
    Vector2 firstPressPosition;
    // スワイプの終点の座標
    Vector2 secondPressPosition;
    // スワイプ量 = 終点 - 起点
    float currentSwipePosition;
    // 回転スピードの調整
    float swipeVector;
    // 画面サイズ / ピクセル
    float screenCorrection;
    // 縦方向の回転軸
    float varticalAngle;
    // 横方向の回転軸
    float horizontalAngle;
    // タッチしている指の数をカウントする
    int deviceTouchCount;

    // ピンチが終了しているかの判定: ２本目が離れたときにfalseにする
    bool isMultiTouch = false;

    /**** スワイプ ****/
    // スマホの１本目のタッチ
    Touch touch0;
    // スマホの２本目のタッチ
    Touch touch1;
    // マルチタップ用 配列
    Touch[] multiTouches;

    /**** タップ ****/
    // タップと区別するスワイプ量
    [SerializeField] float swipeMagnitude = 0.05f;

    /**** ダブルタップ ****/
    // tap回数を記録 完成したらはずす
    int doubleTapCount;
    // 直前のタップ時刻
    float lastTapTime;
    // タップ間の時間で判別
    [SerializeField] float doubleTapTimeThreshold = 0.4f;

    /**** 長押し(LongTap) ****/
    // タップの継続時間(フレーム数)
    float startHoldTime;
    // LongTap判定時間
    [SerializeField] float holdMagnitude = 0.1f;

    /**** フリック ****/
    // 最後の1フレームのフリックの長さで判定
    [SerializeField] float flickMagnitude = 25.0f;

    /**** ピンチ ****/
    // ピンチの最初の長さ
    float startDistance;
    // ピンチを動かした長さ
    float baseDistance;
    // ピンチイン、ピンチアウトの判定に使用
    float pinchDistance;

    /**** コルーチン ****/
    // コルーチンを外部から止める
    Coroutine _rotateCoroutine;

    void Awake()
    {
        // キューブを探す
        GameObject gameObject = GameObject.Find(centerObject);
        _transform = gameObject.transform;
        // 回転の補正 (解像度(縦) / １インチあたり画素数) → 　4inc  → 画面スクロールで半周
        screenCorrection = 180 / (Screen.height / Screen.dpi);
    }

    void Start()
    {
        // Start表示
        textHead.text = "H " + Screen.height + ":W " + Screen.width + ":H " + screenCorrection + ":inch";
    }

    void Update()
    {
        // タッチしている指の数を取得
        deviceTouchCount = Input.touchCount;

        // マルチタッチのリセット、タッチの継続を判定・修正
        if (isMultiTouch == true && deviceTouchCount == 0)
        {
            isMultiTouch = false;
            //textBottom.text = "pinch END";
        }

        // 1本タッチ
        if (Input.touchCount == 1 && isMultiTouch == false)
        {
            touch0 = Input.GetTouch(0);

            // Swipe,Tap,Flick
            if (touch0.phase == TouchPhase.Began)
            {
                // スワイプのスタート位置
                startTapPosition = touch0.position;
                // スワイプの始点(初回用)
                firstPressPosition = startTapPosition;
                // 繰り返しスワイプのときに終点を消すため ???? 2023/02/08
                secondPressPosition = startTapPosition;
                // Hold(長押し)測定開始
                startHoldTime = Time.time;
            }
            if (touch0.phase == TouchPhase.Moved)
            {
                // スワイプ中の位置の記録
                secondPressPosition = touch0.position;
                // 縦方向の回転量
                varticalAngle = (secondPressPosition.y - firstPressPosition.y);
                // 横方向は回転方向が逆のため(*-1)と同じ
                horizontalAngle = (firstPressPosition.x - secondPressPosition.x);
                // ベクトル
                swipeVector = (secondPressPosition - firstPressPosition).magnitude;

                // Tapとスワイプの判別
                if (Mathf.Abs((secondPressPosition - startTapPosition).magnitude) > swipeMagnitude)
                {
                    /**** (1_1) ****/
                    OnSwipe();
                    // 始点のリセット
                    firstPressPosition = secondPressPosition;
                }
                if ((Time.time - startHoldTime) > holdMagnitude)
                {
                    textHead.text = "start Hold ";
                    /**** (1__) ****/
                    OnHold();
                    // リセット
                    startHoldTime = 0;
                }
            }
            if (touch0.phase == TouchPhase.Ended)
            {
                // スワイプの終了位置
                endPressPosition = touch0.position;
                // スワイプの幅
                currentSwipePosition = Mathf.Abs((endPressPosition - firstPressPosition).magnitude);
                // Flick
                if (currentSwipePosition > flickMagnitude)
                {
                    // 縦方向の回転量
                    varticalAngle = (endPressPosition.y - firstPressPosition.y);
                    // 横方向は回転方向が逆のため(*-1)と同じ
                    horizontalAngle = (firstPressPosition.x - endPressPosition.x);
                    // スワイプスピードを取得
                    swipeVector = (endPressPosition - firstPressPosition).magnitude;
                    /**** (1/) ****/
                    OnFlick();
                }
                // Tap
                else if (currentSwipePosition < swipeMagnitude && (Time.time - startHoldTime) < holdMagnitude)
                {
                    // ダブルタップ
                    doubleTapCount++; // このカウントはいらない
                    textBottom.text = doubleTapCount + " : Tap : " + (Time.time - lastTapTime);
                    if ((Time.time - lastTapTime) < doubleTapTimeThreshold)
                    {
                        /**** (1,1) ****/
                        OnDoubleTap();
                        textHead.text = "Double Tap : " + doubleTapCount;
                        doubleTapCount = 0;
                        lastTapTime = 0;
                    }
                    else
                    {
                        /**** ( 1 ) ****/
                        OnTap();

                        textBottom.text = doubleTapCount + " : Single Tap : " + (Time.time - lastTapTime);
                        // ダウルタップ用の時刻入力
                        lastTapTime = Time.time;
                    }
                }
            }
        }
        // ２本タッチ(ピンチイン、ピンチアウト)
        else if (deviceTouchCount == 2 && isMultiTouch == false)
        {
            touch0 = Input.GetTouch(0);
            touch1 = Input.GetTouch(1);
            // ピンチ　最初の位置
            if (touch1.phase == TouchPhase.Began)
            {
                // 初めの指の間隔を記録
                startDistance = Mathf.Abs((touch0.position - touch1.position).magnitude);
            }
            // ピンチを動かしている時
            if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                // タッチした瞬間の指の距離
                baseDistance = Mathf.Abs((touch0.position - touch1.position).magnitude);
                // pinchIN/OUTの判定
                pinchDistance = startDistance - baseDistance;
                /**** ( 2 ) ****/
                OnPinch();
            }
            // ピンチを離した時
            if (Input.touches[0].phase == TouchPhase.Ended || Input.touches[1].phase == TouchPhase.Ended)
            {
                isMultiTouch = true; // すべての指が離れるまでの判定
            }
        }
        // 3本タッチ
        else if (Input.touchCount == 3)
        {
            /**** ( 3 ) ****/
            OnThreefingerTouch();

            isMultiTouch = true; // すべての指が離れるまでの判定
        }
        // ４本タッチ
        else if (Input.touchCount == 4)
        {
            /**** ( 4 ) ****/
            OnFourfingerTouch();

            isMultiTouch = true; // すべての指が離れるまでの判定
        }
    }

    /*************************** 画面操作メソッド ****************************
     * 操作内容：Tap,Hold,DoubleTap,Swipe,Flick,Pinch(in,out),3Tap,4Tap
     *********************************************************************/
    void OnTap()
    {
        // 回転コルーチンを停止する NULLチェックあり
        if (_rotateCoroutine != null)
        {
            StopCoroutine(_rotateCoroutine);
        }

        ChangeCubeColor();
    }
    // 長押し
    void OnHold()
    {
        textHead.text = "On_Hold : " + startHoldTime;
    }
    // ダブルタップ
    void OnDoubleTap()
    {
        textBottom.text = "On_DoubleTap : " + (Time.time - lastTapTime);
    }
    void OnSwipe()
    {
        // 回転スピード screenCorrectionは暫定数　画面サイズで調整予定　2023/02/07
        //transform.Rotate(new Vector3(varticalAngle, horizontalAngle, 0f), swipeVector * Time.deltaTime * screenCorrection, Space.World);
        _transform.rotation = Quaternion.AngleAxis(swipeVector * Time.deltaTime * screenCorrection, new Vector3(varticalAngle, horizontalAngle, 0f)) * _transform.rotation;

        textBottom.text = "On_Swipe";
    }
    void OnFlick()
    {
        // 回転コルーチンの開始
        _rotateCoroutine = StartCoroutine(CubeRotateAsync());

        textBottom.text = currentSwipePosition + " : On_Flick";
    }
    void OnPinch()
    {
        // ピンチイン/ピンチアウト
        MoveCubeLocalPosition(pinchDistance);
    }
    void OnThreefingerTouch()
    {
        // ３本指
        // 回転の判定
        if (_rotateCoroutine != null)
        {
            // 回転コルーチンを止める
            StopCoroutine(_rotateCoroutine);
        }
        // 初期値に戻す(向き)
        _transform.rotation = Quaternion.identity;

        textBottom.text = "MultiTap_3 Test " + Input.touches.Length + " :Tap";
    }
    void OnFourfingerTouch()
    {
        // 4本指
        textBottom.text = "MultiTap_4 Test " + Input.touches.Length + " :Tap";
    }

    /************************ オブジェクトのメソッド ************************
     * 個別の操作
     * ピンチ：キューブ間を広げる
     * タップ：キューブの色を変える
     * フリック：キューブを回転させる(コルーチン)
     ******************************************************************/

    /// <summary>
    /// キューブ間を広げる/戻す
    /// </summary>
    /// <param name="_pinchDistance"></param>
    void MoveCubeLocalPosition(float _pinchDistance)
    {
        for (int i = 1; i < (cubeLength + 1); i++)
        {
            for (int j = 1; j < (cubeLength + 1); j++)
            {
                for (int k = 1; k < (cubeLength + 1); k++)
                {
                    // 3桁のCubNumberを生成
                    cubeNumber = "Cube" + (i * 100 + j * 10 + k).ToString();
                    if (_pinchDistance < 0)
                    {
                        GameObject.Find(cubeNumber).transform.localPosition = new Vector3((i - adjusrNum) * pinchLength, (j - adjusrNum) * pinchLength, (k - adjusrNum) * pinchLength);
                    }
                    else
                    {
                        GameObject.Find(cubeNumber).transform.localPosition = new Vector3((i - adjusrNum) * cubeGap, (j - adjusrNum) * cubeGap, (k - adjusrNum) * cubeGap);
                    }
                }
            }
        }
    }
    /// <summary>
    /// タップしたらキューブの色を変える
    /// </summary>
    void ChangeCubeColor()
    {
        Ray ray = Camera.main.ScreenPointToRay(startTapPosition);
        Vector3 worldPos = ray.direction;
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        {
            // タグ名の取得 名前を取得 
            textBottom.text = "Tap: " + hit.collider.gameObject.tag + " : " + hit.collider.gameObject.name; ;
            // タグ名で判定
            Color cubeColor = hit.collider.gameObject.GetComponent<Renderer>().material.color;
            if (hit.collider.gameObject.tag == "GameCube")
            {
                // 色の変更
                if (cubeColor != Color.blue)
                {
                    cubeColor = Color.blue;
                }
                else if (cubeColor == Color.blue)
                {
                    cubeColor = Color.red;
                }
                else if (cubeColor == Color.red)
                {
                    cubeColor = Color.white;
                }
                else
                {
                    cubeColor = Color.white;
                }
                hit.collider.gameObject.GetComponent<Renderer>().material.color = cubeColor;
            }
        }
    }
    /// <summary>
    /// フリックした時の処理 <br/>
    /// 回転継続
    /// </summary>
    /// <returns></returns>
    private IEnumerator CubeRotateAsync()
    {
        textBottom.text = swipeVector + "Flick test : Start";
        // 回転を続ける
        Vector2 touchDeltaPosition = touch0.deltaPosition;

        while (Mathf.Abs(swipeVector) >= 1.0f)
        {
            // 減速させる
            swipeVector *= 0.97f;

            textHead.text = "Flick test : " + swipeVector;
            // スロー回転
            _transform.rotation = Quaternion.AngleAxis(swipeVector * Time.deltaTime * screenCorrection, new Vector3(varticalAngle, horizontalAngle, 0f)) * _transform.rotation;

            //_transform.Rotate(new Vector3(varticalAngle, horizontalAngle, 0f), swipeVector * Time.deltaTime * screenCorrection, Space.World);
            // １フレーム待機する
            yield return null;
        }
        //yield return new WaitForSeconds(2.0f);
    }
}



