﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoutePanelBehavior : MonoBehaviour {

    // Use this for initialization
    RoadDrawing drawing;
    Vector3? start;
    Vector3? end;
    public GameObject carModelPrefab;

    enum mode { listenStart, listenEnd, none};
    mode workingMode;

    public GameObject indicatorPrefab;
    GameObject indicator;

    GameObject vehicle;

    void Start () {
        workingMode = mode.none;
        setStartFlag();
        start = end = null;
        indicator = Instantiate(indicatorPrefab, Vector3.zero, Quaternion.identity);
        drawing = GameObject.Find("curveIndicator").GetComponent<RoadDrawing>();
    }

    // Update is called once per frame
    void Update () {
        indicator.transform.position = GameObject.FindWithTag("MainCamera").GetComponent<RayHit>().hitpoint3;
        Vector3 position = GameObject.FindWithTag("MainCamera").GetComponent<RayHit>().hitpoint3;
        Road r;
        position = drawing.roadManager.approxNodeToExistingRoad(position, out r);
        indicator.transform.position = position;

        switch (workingMode){
            case mode.listenStart:
                indicator.transform.localScale = Vector3.one;
                if (Input.GetMouseButtonDown(1)){
                    if (r != null){
                        workingMode = mode.none;
                        setFinishFlag();
                        start = position;
                    }
                    else{
                        start = null;;
                    }
                }
                break;
            case mode.listenEnd:
                indicator.transform.localScale = Vector3.one;
                if (Input.GetMouseButtonDown(1))
                {
                    if (r != null)
                    {
                        workingMode = mode.none;
                        setStartFlag();
                        end = position;
                        placeVehicle();
                    }
                    else
                    {
                        end = null;
                    }
                }
                break;
            case mode.none:
                indicator.transform.localScale = Vector3.zero;
                break;
        }  
    }

    void setStartFlag(){
        transform.GetChild(0).GetChild(0).GetComponent<Image>().sprite = Resources.Load<Sprite>("Textures/startflag");
    }

    void setFinishFlag(){
        transform.GetChild(0).GetChild(0).GetComponent<Image>().sprite = Resources.Load<Sprite>("Textures/finishflag");
    }

    public void enterListenMode(){
        GameObject.Find("Canvas").GetComponent<UIController>().ResetDrawing();

        if (start == null){
            workingMode = mode.listenStart;
        }
        else{
            if (end == null){
                workingMode = mode.listenEnd;
            }
        }
        Debug.Log("set working mode to " + workingMode);
    }

    void placeVehicle(){
        vehicle = Instantiate(carModelPrefab);
        vehicle.GetComponent<Vehicle>().SetStart(start.Value);
        vehicle.GetComponent<Vehicle>().SetDest(end.Value);
    }

    public void toggleRouteView(){
        vehicle.GetComponent<Vehicle>().toggleRouteView();
    }
}
