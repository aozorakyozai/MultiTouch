using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System;
//using static System.Net.Mime.MediaTypeNames;


public class Cube333 : MonoBehaviour
{
    // 左下テキスト
    [SerializeField] TextMeshProUGUI textHead;
    [SerializeField] TextMeshProUGUI textBottom;

    /**** Cube構造設定 ****/
    /// <summary>
    /// 中央のキューブの指定 <br/>
    /// 3x3x3の場合、[cube222]
    /// </summary>
    string cubeCenter = "Cube222";
    /// <summary>
    /// transformに毎回アクセスすると重くなるから、キャッシュするため
    /// </summary>
    private Transform _transform;
    /// <summary>
    /// デフォルト位置のキューブ間の距離 <br/>
    /// Defalt 1.1
    /// </summary>
    [SerializeField] float cubeGap = 1.1f;
    /// <summary>
    /// Childキューブを広げた時のキューブ間の距離 <br/>
    /// Defalt 2.2
    /// </summary>
    [SerializeField] float pinchLength = 2.2f;
    /// <summary>
    /// Childキューブ用
    /// ParentCubeに対して x+2,y+2,z+2
    /// </summary>
    string cubeNumber;

    /**** MultiTouch座標設定 ****/
    /// <summary>
    /// 始点の座標
    /// </summary>
    Vector2 startTapPosition;
    /// <summary>
    /// 終点の座標
    /// </summary>
    Vector2 EndPressPosition;
    /// <summary>
    /// スワイプの起点の座標
    /// </summary>
    Vector2 firstPressPosition;
    /// <summary>
    /// スワイプの終点の座標
    /// </summary>
    Vector2 secondPressPosition;
    /// <summary>
    /// スワイプ量 = 終点 - 起点
    /// </summary>
    float currentSwipePosition;
    /// <summary>
    /// 回転スピードの調整
    /// </summary>
    float swipeVector;
    /// <summary>
    /// 画面サイズ / ピクセル
    /// </summary>
    float screenCorrection;
    /// <summary>
    /// 縦方向の回転軸
    /// </summary>
    float varticalAngle;
    /// <summary>
    /// 横方向の回転軸
    /// </summary>
    float horizontalAngle;
    /// <summary>
    /// マルチタップ用 <br/>
    /// タッチしている指の数をカウントする
    /// </summary>
    int deviceTouchCount;
    /// <summary>
    /// ピンチが終了しているかの判定 <br/>
    /// ２本目が離れたときにfalseにする
    /// </summary>
    bool isMultiTouch = false;

    /**** スワイプ ****/
    /// <summary>
    /// スマホの１本目のタッチ
    /// </summary>
    Touch touch0;
    /// <summary>
    /// スマホの２本目のタッチ
    /// </summary>
    Touch touch1;
    /// <summary>
    /// マルチタップ用 配列
    /// </summary>
    Touch[] multiTouches;

    /**** タップ ****/
    /// <summary>
    /// タップと区別するスワイプ量
    /// </summary>
    [SerializeField] float swipeMagnitude = 0.05f;

    /**** フリック ****/
    /// <summary>
    /// 最後の1フレームのフリックの長さで判定
    /// </summary>
    [SerializeField] float flickMagnitude = 35.0f;

    /**** ピンチ ****/
    /// <summary>
    /// ピンチの最初の長さ
    /// </summary>
    float startDistance;
    /// <summary>
    /// ピンチを動かした長さ
    /// </summary>
    float baseDistance;
    /// <summary>
    /// ピンチイン、ピンチアウトの判定に使用
    /// </summary>
    float pinchDistance;

    /**** コルーチン ****/
    /// <summary>
    /// コルーチンを外部から止める
    /// </summary>
    Coroutine _rotateCoroutine;

    void Awake()
    {
        // キューブを探す
         GameObject gameObject = GameObject.Find(cubeCenter);
         _transform = gameObject.transform;
        // 回転の補正 (解像度(縦) / １インチあたり画素数) → 　4inc  → 画面スクロールで半周
        screenCorrection = 180 / (Screen.height / Screen.dpi);
    }

    void Start()
    {
        // Start表示
        textHead.text = "H" + Screen.height + ":W" + Screen.width + ":H" + Math.Round(screenCorrection, 1, MidpointRounding.AwayFromZero) + "inch";
    }

    void Update()
    {
        // タッチしている指の数を取得
        deviceTouchCount = Input.touchCount;

        // マルチタッチのリセット、タッチの継続を判定・修正
        if (isMultiTouch == true && deviceTouchCount == 0)
        {
            isMultiTouch = false;
            textBottom.text = "pinch END";
        } 
        
        // 1タッチ
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
                    OnSwipe();
                }
            }
            if (touch0.phase == TouchPhase.Ended)
            {
                // スワイプの終了位置
                EndPressPosition = touch0.position;
                // スワイプの幅
                currentSwipePosition = Mathf.Abs((EndPressPosition - firstPressPosition).magnitude);
                // Flick
                if (currentSwipePosition > flickMagnitude)
                {
                    // 縦方向の回転量
                    varticalAngle = (EndPressPosition.y - firstPressPosition.y);
                    // 横方向は回転方向が逆のため(*-1)と同じ
                    horizontalAngle = (firstPressPosition.x - EndPressPosition.x);
                    // スワイプスピードを取得
                    swipeVector = (EndPressPosition - firstPressPosition).magnitude;

                    OnFlick();
                }
                // Tap
                else if (currentSwipePosition < swipeMagnitude)
                {
                    OnTap();
                }
            }
        }
        // ピンチ
        else if (deviceTouchCount == 2 && isMultiTouch == false)
        {
            touch0 = Input.GetTouch(0);
            touch1 = Input.GetTouch(1);
            // ピンチ　最初の位置
            if (touch1.phase == TouchPhase.Began)
            {
                startDistance = Mathf.Abs((touch0.position - touch1.position).magnitude);
                textHead.text = startDistance + " pinch Start";
            }
            // ピンチを動かしている時
            if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                // タッチした瞬間の指の距離
                baseDistance = Mathf.Abs((touch0.position - touch1.position).magnitude);
                // pinchIN/OUTの判定
                pinchDistance = startDistance - baseDistance;

                OnPinch();
            }
            // ピンチを離した時
            if (Input.touches[0].phase == TouchPhase.Ended || Input.touches[1].phase == TouchPhase.Ended)
            {
                isMultiTouch = true;
                textBottom.text = "pinch One";
            }
        }
        // 3本・４本 
        else if (Input.touchCount >= 3)
        {
            multiTouches = Input.touches;

            OnMultiTouch();

            isMultiTouch = true;
        }
    }

    /**************** 画面操作メソッド ****************/
    void OnTap()
    {
        // 回転コルーチンを停止する NULLチェックあり
        if (_rotateCoroutine != null)
        {
            StopCoroutine(_rotateCoroutine);
        }

        ChangeCubeColor();
        // 回転を停止
        //transform.Rotate(Vector3.zero, 0f, Space.World);
        // いらないのでは　2023/02/08
        //_transform.rotation = Quaternion.AngleAxis(0f, new Vector3(varticalAngle, horizontalAngle, 0f)) * _transform.rotation;
    }
    void OnSwipe()
    {
        // 回転スピード screenCorrectionは暫定数　画面サイズで調整予定　2023/02/07
        //transform.Rotate(new Vector3(varticalAngle, horizontalAngle, 0f), swipeVector * Time.deltaTime * screenCorrection, Space.World);
        _transform.rotation = Quaternion.AngleAxis(swipeVector * Time.deltaTime * screenCorrection, new Vector3(varticalAngle, horizontalAngle, 0f)) * _transform.rotation;
        // 始点のリセット
        firstPressPosition = secondPressPosition;
        textBottom.text = "Swipe";
    }
    void OnFlick()
    {
        // 回転コルーチンの開始
        _rotateCoroutine = StartCoroutine(FlickAction());
        textBottom.text = currentSwipePosition + " : Flick";
    }
    void OnPinch()
    {
        MoveCubeLocalPosition(pinchDistance);
    }
    void OnMultiTouch()
    {
        // 回転の判定
        if (_rotateCoroutine != null)
        {
            // 回転コルーチンを止める
            StopCoroutine(_rotateCoroutine);
        }
        // ３本
        if (multiTouches.Length == 3)
        {
            // 初期値に戻す(向き)
            _transform.rotation = Quaternion.identity;
            textBottom.text = "MultiTap Test " + multiTouches.Length + ":Tap";
        }
        // ４本
        if (multiTouches.Length == 4)
        {
            // 未設定
            textBottom.text = "MultiTap Test 4";
        }
    }
    /**************** メソッド ****************/
    /// <summary>
    /// キューブ間を広げる/戻す
    /// </summary>
    /// <param name="_pinchDistance"></param>
    void MoveCubeLocalPosition(float _pinchDistance)
    {
        for (int i = 1; i < 4; i++)
        {
            for (int j = 1; j < 4; j++)
            {
                for (int k = 1; k < 4; k++)
                {
                    cubeNumber = "Cube" + (i * 100 + j * 10 + k).ToString();
                    if (_pinchDistance < 0)
                    {
                        GameObject.Find(cubeNumber).transform.localPosition = new Vector3((i - 2) * pinchLength, (j - 2) * pinchLength, (k - 2) * pinchLength);
                    }
                    else
                    {
                        GameObject.Find(cubeNumber).transform.localPosition = new Vector3((i - 2) * cubeGap, (j - 2) * cubeGap, (k - 2) * cubeGap);
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
    private IEnumerator FlickAction()
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


