/********************************************************************
 * 最終更新：2023/04/17　AjustCubeの機能の持続性を拡大
 * 機能：スマホ画面操作 【 CubeDriver 】
 * 環境：Unity 2023.3.13f1 LTS
 * 環境：Unity C# 
 * 操作内容：Tap,DoubleTap,Hold,Swipe,Flick,PinchOut,PinchIn,3Tap,4Tap
 * ３Dオブジェクトをワールド座標で回転させる
 * cubeLength:1辺のCube個数,cubeGap:隙間の大きさを入力すること
 * タップした場所のオブジェクトの色を変える
 * 注意：複数タップ状態からシングルタップ、スワイプ、フリックに継続しない仕様
*********************************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using Unity.VisualScripting;

public class CubeDriver : MonoBehaviour
{
    /**** Cube構造設定 ****/
    /// <summary> 中央のキューブの指定</summary>
    [SerializeField] public string centerObject = "CenterCube";
    /// <summary> transformに毎回アクセスすると重くなるから、キャッシュするため</summary>
    private Transform _transform;
    /// <summary> 一辺のCube数</summary>
    private int cubeLength = 3;
    /// <summary> cubeNameと座標の調整数
    float adjusrNum { get { return (cubeLength + 1) / 2f; } }
    /// <summary> デフォルト位置のキューブ間の距離 (1mのCubeの場合：1.1は,1m + 0.1mの隙間がある)</summary>
    [SerializeField] float cubeGap = 1.1f;
    /// <summary> Childキューブを広げた時のキューブ間の距離</summary>
    float pinchLength { get { return cubeGap * 2f; } }
    /// <summary>問題用キューブ</summary>
    public Color questionColor = GameManager.QuestionColor;
    /// <summary> Childキューブ用 ParentCubeに対して x+2,y+2,z+2</summary>
    string cubeNumber;
    NumberDriver numberDriver = new NumberDriver();
    GameManager gameManager = new GameManager();
    ScreenManagaer screenManagaer = new ScreenManagaer();
    // 選択したキューブ
    public static GameObject tradeCubeAdress;
    int deviceRotate;

    /**** MultiTouch座標設定 ****/
    /// <summary> 始点の座標</summary>
    Vector2 startTapPosition;
    /// <summary> 終点の座標</summary>
    Vector2 endPressPosition;
    /// <summary> スワイプの起点の座標</summary>
    Vector2 firstPressPosition;
    /// <summary> スワイプの終点の座標</summary>
    Vector2 secondPressPosition;
    /// <summary> スワイプ量 = 終点 - 起点</summary>
    float currentSwipePosition;
    /// <summary> 回転スピードの調整</summary>
    float swipeVector;
    /// <summary> 画面サイズ/ピクセル</summary>
    float screenCorrection;
    /// <summary> 縦方向の回転軸</summary>
    float varticalAngle;
    /// <summary> 横方向の回転軸</summary>
    float horizontalAngle;
    /// <summary> タッチしている指の数をカウントする</summary>
    int deviceTouchCount;
    /// <summary> ピンチが終了しているかの判定: ２本目が離れたときにfalseにする</summary>
    bool isMultiTouch = false;

    /**** スワイプ ****/
    /// <summary> スマホの１本目のタッチ</summary>
    Touch touch0;
    /// <summary> スマホの２本目のタッチ</summary>
    Touch touch1;
    /// <summary> マルチタップ用 配列</summary>
    Touch[] multiTouches;

    /**** タップ ****/
    /// <summary> タップと区別するスワイプ量 0.05f</summary>
    float swipeMagnitude = 0.05f;

    /**** ダブルタップ ****/
    /// <summary> tap回数を記録 完成したらはずす</summary>
    int doubleTapCount;
    /// <summary> 直前のタップ時刻</summary>
    float lastTapTime;
    /// <summary> タップ間の時間で判別 0.4f</summary>
    float doubleTapTimeThreshold = 0.4f;

    /**** 長押し(LongTap) ****/
    /// <summary>Hold判定</summary>
    bool isHold;
    /// <summary> タップの継続時間(フレーム数)</summary>
    float startHoldTime;
    /// <summary> LongTap判定時間 1.0f</summary>
    float holdMagnitude = 0.7f;

    /**** フリック ****/
    /// <summary> 最後の1フレームのフリックの長さで判定 25.0f</summary>
    float flickMagnitude = 25.0f;

    /**** ピンチ ****/
    /// <summary> ピンチの最初の長さ</summary>
    float startDistance;
    /// <summary> ピンチを動かした長さ</summary>
    float baseDistance;
    /// <summary> ピンチイン、ピンチアウトの判定に使用</summary>
    float pinchDistance;

    /**** コルーチン ****/
    /// <summary> コルーチンを外部から止める</summary>
    Coroutine _rotateCoroutine;

    /// <summary>回転の継続</summary>
    Vector2 touchDeltaPosition;

    /// <summary>cubeが整列しているか</summary>
    bool isRotateAjustCube;
    /// <summary>スワイプの角度</summary>
    float SwipeAngle;
    /// <summary>スワイプの角度計算用</summary>
    float tanXY = 75f;
    bool isX = false;
    bool isY = false;

    void Awake()
    {
        // 中心のキューブを探す
        GameObject gameObject = GameObject.Find(centerObject);
        _transform = gameObject.transform;
        // 回転の補正 (解像度(縦) / １インチあたり画素数) → 　4inc  → 画面スクロールで半周
        screenCorrection = 180 / (Screen.height / Screen.dpi);
    }

    void Start()
    {
        // インターフェースの使用
    }

    void Update()
    {
        // タッチしている指の数を取得
        deviceTouchCount = Input.touchCount;
        // マルチタッチのリセット、タッチの継続を判定・修正
        if (isMultiTouch == true && deviceTouchCount == 0)
        {
            isMultiTouch = false;
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
                // Hold判定
                isHold = true;
                // Hold(長押し)測定開始
                startHoldTime = Time.time;
            }
            if (touch0.phase == TouchPhase.Moved)
            {
                // スワイプ中の位置の記録
                secondPressPosition = touch0.position;
                // Tapとスワイプの判別 移動量がswipeMagnitudeより大きい場合
                if (Mathf.Abs((secondPressPosition - startTapPosition).magnitude) > swipeMagnitude)
                {
                    /**** (1_1) ****/
                    OnSwipe();
                    // 始点のリセット
                    firstPressPosition = secondPressPosition;
                }
                // スワイプでも反応する　うまくいかない　2023/04/07　100 → 10
                // スワイプで反応してしまうため、100で適用範囲を拡大した
                if (Mathf.Abs((secondPressPosition - startTapPosition).magnitude) < swipeMagnitude * 10)
                {
                    // Holdはあまりうまく反応しない　2023/03/28
                    if ((Time.time - startHoldTime) > holdMagnitude)
                    {
                        if (isHold == true)
                        {
                            /**** (1__) ****/
                            OnHold();
                            isHold = false;
                        }
                    }
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
                    /**** (1/) ****/
                    OnFlick();
                }
                // Tap
                else if (currentSwipePosition < swipeMagnitude && (Time.time - startHoldTime) < holdMagnitude)
                {
                    // ダブルタップ
                    //doubleTapCount++; // このカウントはいらない
                    if ((Time.time - lastTapTime) < doubleTapTimeThreshold)
                    {
                        /**** (1,1) ****/
                        OnDoubleTap();
                        //doubleTapCount = 0;
                        lastTapTime = 0;
                    }
                    else
                    {
                        /**** ( 1 ) ****/
                        OnTap();
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
        // 回転するコルーチンを停止する
        if (_rotateCoroutine != null)
        {
            StopCoroutine(_rotateCoroutine);
        }
        // Cubeの色を変える
        ChangeTapCubeColor();
        // Number表示
        numberDriver.GetNumberObjectName();
    }
    // 長押し
    void OnHold()
    {
        Handheld.Vibrate();
    }
    // ダブルタップ
    void OnDoubleTap()
    {
        // ajust Rotate
        AjustCubeRotate();
    }
    void OnSwipe()
    {
        // 最後に正規化する予定 → スワイプと直結する　2023/03/26
        // 回転スピード screenCorrectionは暫定数　画面サイズで調整予定　2023/02/07
        //_transform.rotation = Quaternion.AngleAxis(swipeVector * Time.deltaTime * screenCorrection, new Vector3(varticalAngle, horizontalAngle, 0f)) * _transform.rotation;
        OnSwipeAjustAngle();
        Debug.Log("swipe");
    }
    void OnFlick()
    {
        // 回転コルーチンの開始
        _rotateCoroutine = StartCoroutine(CubeRotateAsync());
    }
    void OnPinch()
    {
        // ピンチイン/ピンチアウト
        MoveCubeLocalPosition(pinchDistance);
    }
    void OnThreefingerTouch()
    {
        // ３本指
        // 回転するコルーチンを停止する
        if (_rotateCoroutine != null)
        {
            StopCoroutine(_rotateCoroutine);
        }
        // 初期値に戻す
        AjustCubeRotateIdentity();
    }
    void OnFourfingerTouch()
    {
        // 4本指
    }

    /************************ オブジェクトのメソッド ************************
     * 個別の操作
     * タップ：キューブの色を変える
     * キューブの色を変更するメソッド
     * フリック：キューブを回転させる(コルーチン)
     * ピンチ：キューブ間を広げる
     * Cubeの向きをAjust
     * Cubeの回転をAjust
     * Cubeの回転を画面の向きに合わせて初期値に戻す
     ********************************************************************/

    /// <summary>
    /// タップしたキューブの色を変える
    /// </summary>
    void ChangeTapCubeColor()
    {
        Ray ray = Camera.main.ScreenPointToRay(startTapPosition);
        Vector3 worldPos = ray.direction;
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity))
        {
            // タグ名で判定
            if (hit.collider.gameObject.tag == "GameCube")
            {
                tradeCubeAdress = hit.collider.gameObject;
                //Debug.Log("hit.collider: " + tradeCubeAdress.name);
                Color cubeColor = tradeCubeAdress.GetComponent<Renderer>().material.color;

                // 色の変更
                if (cubeColor == Color.white)
                {
                    // 選択Cube以外を白に戻す
                    ChangeCubeColor(Color.red, Color.white);
                    // 赤にする
                    cubeColor = Color.red;
                    // 選択されたキューブ番号
                    NumberDriver.cubeAdress = cubeNumber; // いらないのでは？
                    //Debug.Log("" + tradeCubeAdress.name);
                    //gameManager.tradeCubeAdress = tradeCubeAdress;
                    //Debug.Log("GameManager: " + tradeCubeAdress.name);
                }
                else if (cubeColor == Color.red)
                {
                    cubeColor = Color.white;
                    NumberDriver.cubeAdress = null;
                    gameManager._tradeCubeAdress = null;
                }
                else if (cubeColor == questionColor)
                {
                    // 出題キューブ以外を白に戻す
                    ChangeCubeColor(Color.red, Color.white);
                    // 選択されたキューブ番号
                    NumberDriver.cubeAdress = cubeNumber;  // いらないのでは？
                    gameManager._tradeCubeAdress = tradeCubeAdress;
                    //Handheld.Vibrate();
                    // 出題の場合は変更しない
                    return;
                }
                else
                {
                    cubeColor = Color.white;
                }
                tradeCubeAdress.GetComponent<Renderer>().materials[0].color = cubeColor;
            }
        }
    }

    /// <summary>
    /// キューブを全検索して指定の色を変更する
    /// </summary>
    /// <param name="変更前の色"></param>
    /// <param name="変更後の色"></param>
    private void ChangeCubeColor(Color beforeColor, Color afterColor)
    {
        for (int i = 0; i < GameManager.cubeGameObjects.Length; i++)
        {
            //Debug.Log(GameManager.cubeGameObjects[i].name);
            if (GameManager.cubeGameObjects[i].transform.GetComponent<Renderer>().materials[0].color == beforeColor)
            {
                GameManager.cubeGameObjects[i].transform.GetComponent<Renderer>().materials[0].color = afterColor;
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
        // 縦方向の回転量
        varticalAngle = (endPressPosition.y - firstPressPosition.y);
        // 横方向は回転方向が逆のため(*-1)と同じ
        horizontalAngle = (firstPressPosition.x - endPressPosition.x);
        // スワイプスピードを取得
        swipeVector = (endPressPosition - firstPressPosition).magnitude;
        // 回転を続ける
        touchDeltaPosition = touch0.deltaPosition;

        while (Mathf.Abs(swipeVector) >= 0.2f)
        {
            // 減速させる
            if (swipeVector > 100)
            {
                swipeVector *= 0.5f;
            }
            else if (swipeVector > 7)
            {
                swipeVector *= 0.7f;
            }
            else
            {
                swipeVector *= 0.999f;
            }
            // スロー回転
            _transform.rotation = Quaternion.AngleAxis(swipeVector * Time.deltaTime * screenCorrection, new Vector3(varticalAngle, horizontalAngle, 0f)) * _transform.rotation;
            // １フレーム待機する
            yield return null;
        }
        //yield return new WaitForSeconds(2.0f);
    }

    /// <summary>
    /// キューブ間を広げる/戻す
    /// </summary>
    /// <param name="_pinchDistance"></param>
    void MoveCubeLocalPosition(float _pinchDistance)
    {
        // Cube１つの場合は抜ける
        if (cubeLength <= 1)
        {
            return;
        }
        int l = 0;
        for (int i = 1; i < (cubeLength + 1); i++)
        {
            for (int j = 1; j < (cubeLength + 1); j++)
            {
                for (int k = 1; k < (cubeLength + 1); k++)
                {
                    if (_pinchDistance < 0)
                    {
                        GameManager.cubeGameObjects[l++].transform.localPosition = new Vector3((i - adjusrNum) * pinchLength, (j - adjusrNum) * pinchLength, (k - adjusrNum) * pinchLength);
                    }
                    else
                    {
                        GameManager.cubeGameObjects[l++].transform.localPosition = new Vector3((i - adjusrNum) * cubeGap, (j - adjusrNum) * cubeGap, (k - adjusrNum) * cubeGap);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 回転方向をアジャストする機能
    /// </summary>
    private void AjustCubeRotate()
    {
        Vector3 rotationCube = _transform.rotation.eulerAngles;
        //Debug.Log("test " + (int)rotation.x + " " + (int)rotation.y + " " + (int)rotation.z);
        float xAjust = AjustRotate(rotationCube.x);
        float yAjust = AjustRotate(rotationCube.y);
        float zAjust = AjustRotate(rotationCube.z);
        _transform.rotation = Quaternion.Euler(xAjust, yAjust, zAjust);
        // 向きが整列している状態
        isRotateAjustCube = true;
        //Debug.Log("test " + rotation.x + " " + rotation.y + " " + rotation.z + " ");
    }

    /// <summary>
    /// 回転量を正規化する機能
    /// </summary>
    /// <param name="現在の回転角度"></param>
    /// <returns>90の倍数に正規化された回転角度</returns>
    private float AjustRotate(float an)
    {
        // 角度を0から360の範囲に正規化する
        //angle = Mathf.Repeat(angle, 360f);

        // 角度を45以上から135未満は90度に、
        // 135以上から225未満は180度に、
        // 225以上から315未満は270度に、
        // それ以外の角度は0度にする
        if (an >= 45f && an < 135f)
        {
            an = 90f;
        }
        else if (an >= 135f && an < 225f)
        {
            an = 180f;
        }
        else if (an >= 225f && an < 315f)
        {
            an = 270f;
        }
        else
        {
            an = 0f;
        }
        return an;
    }

    /// <summary>
    /// ｘｙ方向のスワイプを一定量まで正規化する
    /// </summary>
    /// <param name="スワイプの角度"></param>
    /// <returns></returns>
    private void OnSwipeAjustAngle()
    {
        // 縦方向の回転量
        varticalAngle = (secondPressPosition.y - firstPressPosition.y);
        // 横方向は回転方向が逆のため(*-1)と同じ
        horizontalAngle = (firstPressPosition.x - secondPressPosition.x);
        // ベクトル
        swipeVector = (secondPressPosition - firstPressPosition).magnitude;
        // x/y
        SwipeAngle = Mathf.Atan(varticalAngle / horizontalAngle) * Mathf.Rad2Deg; // 角度を計算する
        //Debug.Log("test 角度は " + SwipeAngle + " 度です。");
        tanXY = 45f;
        // 3本Tap後の状態を維持している場合は、まっすぐ回転させる
        if (isRotateAjustCube)
        {
            // スタートで縦横どちらに回転するか決定する
            if (isY == false && isX == false)
            {
                if (Mathf.Abs(SwipeAngle) < 45)
                {
                    // y軸回転
                    //Debug.Log("test x");
                    isY = true;
                }
                else if (Mathf.Abs(SwipeAngle) >= 45)
                {
                    // x軸回転
                    //Debug.Log("test y");
                    isX = true;
                }
                else
                {
                    isX = false;
                    isY = false;
                }
            }
            // 回転方向が容易に変更されないように60までに適用範囲を拡大する
            if (isY == true)
            {
                if (Mathf.Abs(SwipeAngle) < tanXY)
                {
                    // y軸回転
                    varticalAngle = 0f;
                }
                else
                {
                    isY = false;
                    isRotateAjustCube = false;
                }
            }
            else if (isX == true)
            {
                if (Mathf.Abs(SwipeAngle) > (90 - tanXY))
                {
                    // x軸回転
                    horizontalAngle = 0f;
                }
                else
                {
                    isX = false;
                    isRotateAjustCube = false;
                }
            }
        }
        //Debug.Log("test " + SwipeAngle + " " + isRotateAjustCube + " " + varticalAngle + " " + horizontalAngle);
        _transform.rotation = Quaternion.AngleAxis(swipeVector * Time.deltaTime * screenCorrection, new Vector3(varticalAngle, horizontalAngle, 0f)) * _transform.rotation;
    }
    /// <summary>
    /// Cubeの回転を初期値に戻す <br/>
    /// デバイスの向きを考慮する
    /// </summary>
    void AjustCubeRotateIdentity()
    {
        deviceRotate = screenManagaer.GetOrientation();
        // 回転を初期値に戻す(向き)
        _transform.rotation = Quaternion.Euler(0, 0, deviceRotate);
        //_transform.rotation = Quaternion.identity;
    }
}
