﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum IndicatorType { line, arc, bezeir, none, delete };

public class RoadDrawing : MonoBehaviour
{
    public List<string> laneConfig;

    public GameObject nodeIndicatorPrefab;

    public GameObject roadIndicatorPrefab;

    public GameObject roadManagerPrefab;

    protected GameObject nodeIndicator, roadIndicator;

    public RoadManager roadManager;

    public GameObject degreeTextPrefab;

    List<GameObject> degreeTextInstance, neighborIndicatorInstance;

    public float textDistance;

    public Vector2[] controlPoint;

    Road targetRoad;

    public int pointer;

    public IndicatorType indicatorType;

    public GameObject preview;

    List<Curve> interestedApproxLines;

    public void fixControlPoint(Vector2 cp)
    {
        //call after setControlPoint is called
        pointer++;
    }

    public void setControlPoint(Vector2 cp)
    {
        cp = roadManager.approxNodeToExistingRoad(cp, out targetRoad, interestedApproxLines);
        if (pointer <= 3)
            controlPoint[pointer] = cp;
    }

    public void Awake()
    {
        GameObject manager = Instantiate(roadManagerPrefab, Vector3.zero, Quaternion.identity);
        roadManager = manager.GetComponent<RoadManager>();
        indicatorType = IndicatorType.none;
        controlPoint = new Vector2[4];
        interestedApproxLines = new List<Curve>();
        degreeTextInstance = new List<GameObject>();
        neighborIndicatorInstance = new List<GameObject>();
        reset();
    }


    public void reset()
    {
        for (int i = 0; i != 4; ++i){
            controlPoint[i] = Vector2.negativeInfinity;
        }
        pointer = 0;
        Destroy(nodeIndicator);
        Destroy(roadIndicator);
        interestedApproxLines.Clear();
        clearAngleDrawing();
    }

    public void Update()
    {
        clearAngleDrawing();
        laneConfig = GameObject.FindWithTag("UI/laneconfig").GetComponent<LaneConfigPanelBehavior>().laneconfigresult;
        interestedApproxLines.Clear();


        if (pointer >= 1)
        {
            interestedApproxLines.Add(new Line(controlPoint[pointer - 1] + Vector2.down * Algebra.InfLength, controlPoint[pointer - 1] + Vector2.up * Algebra.InfLength, 0f, 0f));
            interestedApproxLines.Add(new Line(controlPoint[pointer - 1] + Vector2.left * Algebra.InfLength, controlPoint[pointer - 1] + Vector2.right * Algebra.InfLength, 0f, 0f));
            if (targetRoad != null)
            {
                interestedApproxLines.Add(new Line(controlPoint[pointer - 1], targetRoad.curve.AttouchPoint(controlPoint[pointer - 1]), 0f, 0f));
            }

        }

        if (controlPoint[pointer].x != Vector3.negativeInfinity.x && indicatorType != IndicatorType.none)
        {
            Destroy(nodeIndicator);
            nodeIndicator = Instantiate(nodeIndicatorPrefab, new Vector3(controlPoint[pointer].x, 0f, controlPoint[pointer].y), Quaternion.identity);
            nodeIndicator.transform.SetParent(transform);

            if (indicatorType == IndicatorType.line)
            {

                if (pointer == 1)
                {
                    Destroy(roadIndicator);
                    addAngleDrawing(controlPoint[1], controlPoint[0]);
                    addAngleDrawing(controlPoint[0], controlPoint[1]);

                    Road cp0_targetRoad;
                    roadManager.approxNodeToExistingRoad(controlPoint[0], out cp0_targetRoad);
                    if (cp0_targetRoad != null){
                        //perpendicular
                        interestedApproxLines.Add(new Line(controlPoint[0], controlPoint[0] + Algebra.angle2dir(cp0_targetRoad.curve.angle_2d((float)cp0_targetRoad.curve.paramOf(controlPoint[0])) + Mathf.PI / 2) * Algebra.InfLength, 0f, 0f));
                        interestedApproxLines.Add(new Line(controlPoint[0], controlPoint[0] + Algebra.angle2dir(cp0_targetRoad.curve.angle_2d((float)cp0_targetRoad.curve.paramOf(controlPoint[0])) - Mathf.PI / 2) * Algebra.InfLength, 0f, 0f));
                        //extension
                        if (Algebra.isclose(cp0_targetRoad.curve.at_ending_2d(true), controlPoint[0])){
                            Node crossingRoad;
                            roadManager.findNodeAt(cp0_targetRoad.curve.at_ending(true), out crossingRoad);
                            Debug.Assert(crossingRoad != null);
                            interestedApproxLines.AddRange(crossingRoad.directionalLines(Algebra.InfLength, reverse:true));
                        }
                        else{
                            if (Algebra.isclose(cp0_targetRoad.curve.at_ending_2d(false),controlPoint[0])){
                                Node crossingRoad;
                                roadManager.findNodeAt(cp0_targetRoad.curve.at_ending(false), out crossingRoad);
                                Debug.Assert(crossingRoad != null);
                                interestedApproxLines.AddRange(crossingRoad.directionalLines(Algebra.InfLength, reverse: true));
                            }
                        }

                    }

                    if (!Algebra.isclose((controlPoint[0] - controlPoint[1]).magnitude, 0f))
                    {
                        roadIndicator = Instantiate(roadIndicatorPrefab, transform);
                        RoadRenderer roadConfigure = roadIndicator.GetComponent<RoadRenderer>();
                        roadConfigure.generate(new Line(controlPoint[0], controlPoint[1], 0f, 0f), laneConfig, indicator: true);
                    }
                }

                if (pointer == 2)
                {
                    roadManager.addRoad(new Line(controlPoint[0], controlPoint[1], 0f, 0f), laneConfig);
                    reset();
                }

            }
            if (indicatorType == IndicatorType.bezeir)
            {
                if (pointer == 1){
                    Destroy(roadIndicator);
                    addAngleDrawing(controlPoint[1], controlPoint[0]);
                    addAngleDrawing(controlPoint[0], controlPoint[1]);
                    interestedApproxLines.Add(new Line(controlPoint[0] + Vector2.down * Algebra.InfLength, controlPoint[0] + Vector2.up * Algebra.InfLength, 0f, 0f));
                    interestedApproxLines.Add(new Line(controlPoint[0] + Vector2.left * Algebra.InfLength, controlPoint[0] + Vector2.right * Algebra.InfLength, 0f, 0f));
                    
                    Road cp0_targetRoad;
                    roadManager.approxNodeToExistingRoad(controlPoint[0], out cp0_targetRoad);
                    if (cp0_targetRoad != null)
                    {
                        interestedApproxLines.Add(new Line(controlPoint[0], controlPoint[0] + Algebra.angle2dir(cp0_targetRoad.curve.angle_2d((float)cp0_targetRoad.curve.paramOf(controlPoint[0])) + Mathf.PI / 2) * Algebra.InfLength, 0f, 0f));
                        interestedApproxLines.Add(new Line(controlPoint[0], controlPoint[0] + Algebra.angle2dir(cp0_targetRoad.curve.angle_2d((float)cp0_targetRoad.curve.paramOf(controlPoint[0])) - Mathf.PI / 2) * Algebra.InfLength, 0f, 0f));
                        if (Algebra.isclose(cp0_targetRoad.curve.at_ending_2d(true), controlPoint[0]))
                        {
                            Node crossingRoad;
                            roadManager.findNodeAt(cp0_targetRoad.curve.at_ending(true), out crossingRoad);
                            Debug.Assert(crossingRoad != null);
                            interestedApproxLines.AddRange(crossingRoad.directionalLines(Algebra.InfLength, reverse: true));
                        }
                        else
                        {
                            if (Algebra.isclose(cp0_targetRoad.curve.at_ending_2d(false), controlPoint[0]))
                            {
                                Node crossingRoad;
                                roadManager.findNodeAt(cp0_targetRoad.curve.at_ending(false), out crossingRoad);
                                Debug.Assert(crossingRoad != null);
                                interestedApproxLines.AddRange(crossingRoad.directionalLines(Algebra.InfLength, reverse: true));
                            }
                        }
                    }

                    if (!Algebra.isclose(controlPoint[0], controlPoint[1]))
                    {
                        roadIndicator = Instantiate(roadIndicatorPrefab, transform);
                        RoadRenderer roadConfigure = roadIndicator.GetComponent<RoadRenderer>();
                        roadConfigure.generate(new Line(controlPoint[0], controlPoint[1], 0f, 0f), laneConfig, indicator: true);
                    }
                }

                if (pointer == 2){

                    if (!Geometry.Parallel(controlPoint[1] - controlPoint[0], controlPoint[2] - controlPoint[1])
                        && !Algebra.isRoadNodeClose(controlPoint[2], controlPoint[1]))
                    {
                        Destroy(roadIndicator);
                        addAngleDrawing(controlPoint[2], controlPoint[1]);

                        roadIndicator = Instantiate(roadIndicatorPrefab, transform);
                        RoadRenderer roadConfigure = roadIndicator.GetComponent<RoadRenderer>();
                        roadConfigure.generate(new Bezeir(controlPoint[0], controlPoint[1], controlPoint[2], 0f, 0f), laneConfig, indicator:true);
                    }

                }

                if (pointer == 3){
                    
                    if (!Geometry.Parallel(controlPoint[1] - controlPoint[0], controlPoint[2] - controlPoint[1])
                       && !Algebra.isRoadNodeClose(controlPoint[2], controlPoint[1]))
                    {
                        roadManager.addRoad(new Bezeir(controlPoint[0], controlPoint[1], controlPoint[2], 0f, 0f), laneConfig);
                        reset();
                    }
                    else{
                        pointer = 2;
                    }
                }
            }

            if (indicatorType == IndicatorType.arc){
                if (pointer == 1){

                    Road cp0_targetRoad;
                    roadManager.approxNodeToExistingRoad(controlPoint[0], out cp0_targetRoad);
                    if (cp0_targetRoad != null)
                    {
                        addAngleDrawing(controlPoint[0], controlPoint[1]);

                        if (Algebra.isclose(cp0_targetRoad.curve.at_ending_2d(true), controlPoint[0]))
                        {
                            interestedApproxLines.Add(new Line(controlPoint[0], controlPoint[0] + Algebra.angle2dir(cp0_targetRoad.curve.angle_2d(0f) + Mathf.PI / 2) * Algebra.InfLength, 0f, 0f));
                            interestedApproxLines.Add(new Line(controlPoint[0], controlPoint[0] + Algebra.angle2dir(cp0_targetRoad.curve.angle_2d(0f) - Mathf.PI / 2) * Algebra.InfLength, 0f, 0f));
                            Node crossingRoad;
                            roadManager.findNodeAt(cp0_targetRoad.curve.at_ending(true), out crossingRoad);
                            Debug.Assert(crossingRoad != null);
                            interestedApproxLines.AddRange(crossingRoad.directionalLines(Algebra.InfLength, reverse: true));
                        }
                        else
                        {
                            if (Algebra.isclose(cp0_targetRoad.curve.at_ending_2d(false), controlPoint[0]))
                            {
                                interestedApproxLines.Add(new Line(controlPoint[0], controlPoint[0] + Algebra.angle2dir(cp0_targetRoad.curve.angle_2d(1f) + Mathf.PI / 2) * Algebra.InfLength, 0f, 0f));
                                interestedApproxLines.Add(new Line(controlPoint[0], controlPoint[0] + Algebra.angle2dir(cp0_targetRoad.curve.angle_2d(1f) - Mathf.PI / 2) * Algebra.InfLength, 0f, 0f));
                                Node crossingRoad;
                                roadManager.findNodeAt(cp0_targetRoad.curve.at_ending(false), out crossingRoad);
                                Debug.Assert(crossingRoad != null);
                                interestedApproxLines.AddRange(crossingRoad.directionalLines(Algebra.InfLength, reverse: true));
                            }
                        }
                    }

                    /*ind[0] is start, ind[1] isorigin*/
                    Destroy(roadIndicator);
                    if (!Algebra.isclose((controlPoint[0] - controlPoint[1]).magnitude, 0f))
                    {
                        roadIndicator = Instantiate(roadIndicatorPrefab, transform);
                        RoadRenderer roadConfigure = roadIndicator.GetComponent<RoadRenderer>();
                        roadConfigure.generate(new Line(controlPoint[1], controlPoint[0], 0f, 0f), laneConfig);
                        if (!Algebra.isclose((controlPoint[1] - controlPoint[0]).magnitude, 0))
                            roadConfigure.generate(new Arc(controlPoint[1], controlPoint[0], 1.999f * Mathf.PI, 0f, 0f), laneConfig, indicator: true);
                    }
                }

                if (pointer == 2){
                    Vector2 basedir = controlPoint[0] - controlPoint[1];
                    Vector2 towardsdir = controlPoint[2] - controlPoint[1];
                    interestedApproxLines.Add(new Arc(controlPoint[1], controlPoint[0], Mathf.PI * 1.999f, 0f, 0f));
                    if (!Algebra.isclose(0, towardsdir.magnitude) && !Algebra.isclose(controlPoint[1], controlPoint[0]) && !Geometry.Parallel(basedir, towardsdir))
                    {
                        Destroy(roadIndicator);
                        roadIndicator = Instantiate(roadIndicatorPrefab, transform);
                        RoadRenderer roadConfigure = roadIndicator.GetComponent<RoadRenderer>();
                        roadConfigure.generate(new Arc(controlPoint[1], controlPoint[0], Mathf.Deg2Rad * Vector2.SignedAngle(basedir, towardsdir), 0f, 0f), laneConfig, indicator:true);
                        roadConfigure.generate(new Arc(controlPoint[1], controlPoint[1] + Vector2.right , 1.999f * Mathf.PI, 0f, 0f), laneConfig, indicator:true);
                    }
                }

                if (pointer == 3){
                    Vector2 basedir = controlPoint[0] - controlPoint[1];
                    Vector2 towardsdir = controlPoint[2] - controlPoint[1];
                    if (Algebra.isclose(0, towardsdir.magnitude)){
                        pointer = 2;
                    }
                    else
                    {
                        roadManager.addRoad(new Arc(controlPoint[1], controlPoint[0], Mathf.Deg2Rad * Vector2.SignedAngle(basedir, towardsdir), 0f, 0f), laneConfig);
                        reset();
                    }
                }
            }

            if (indicatorType == IndicatorType.delete){

                if (pointer == 0)
                {
                    Destroy(roadIndicator);

                    if (targetRoad != null)
                    {
                        roadIndicator = Instantiate(roadIndicatorPrefab, transform);
                        RoadRenderer roadConfigure = roadIndicator.GetComponent<RoadRenderer>();
                        roadConfigure.generate(targetRoad.curve, new List<string> { "removal_" + targetRoad.width });
                    }
                }
                else{
                    if (targetRoad != null)
                    {
                        roadManager.deleteRoad(targetRoad);
                        reset();
                    }
                    else{
                        pointer = 0;
                    }
                
                }
            }
        }

    }

    void addAngleDrawing(Vector2 positionMaybeOnRoad, Vector2 anotherPosition){
        Node n;
        List<Vector2> neighborDirs;
        if (roadManager.findNodeAt(new Vector3(positionMaybeOnRoad.x, 0f, positionMaybeOnRoad.y), out n))
        {
            neighborDirs = n.getNeighborDirections(anotherPosition - positionMaybeOnRoad);
        }
        else
        {
            Road tar;
            roadManager.approxNodeToExistingRoad(positionMaybeOnRoad, out tar);
            if (tar == null)
            {
                return;
            }
            float? param = tar.curve.paramOf(positionMaybeOnRoad);
            if (param == null){
                return;
            }
            neighborDirs = new List<Vector2>() { tar.curve.direction_2d((float)param), -tar.curve.direction_2d((float)param) };
        }
        foreach(Vector2 dir in neighborDirs){
            //Debug.Log("angle: " + Vector2.Angle(anotherPosition - positionMaybeOnRoad, dir));
            Vector2 text2dPosition = positionMaybeOnRoad + ((anotherPosition - positionMaybeOnRoad).normalized + dir.normalized).normalized * textDistance;
            //Debug.Log(text2dPosition);
            GameObject textObj = Instantiate(degreeTextPrefab, new Vector3(text2dPosition.x, 0f, text2dPosition.y), Quaternion.Euler(90f, 0f, 0f));
            textObj.transform.SetParent(transform);
            textObj.GetComponent<TextMesh>().text = Mathf.RoundToInt(Mathf.Abs(Vector2.Angle(anotherPosition - positionMaybeOnRoad, dir))).ToString();
            degreeTextInstance.Add(textObj);

            GameObject indicatorObj = Instantiate(roadIndicatorPrefab, transform);
            RoadRenderer indicatorConfigure = indicatorObj.GetComponent<RoadRenderer>();
            indicatorConfigure.generate(new Line(positionMaybeOnRoad, positionMaybeOnRoad + dir * textDistance * 2, 0f, 0f), new List<string> { "dash_blueindi"});
            neighborIndicatorInstance.Add(indicatorObj);

        }

    }

    void clearAngleDrawing(){
        foreach(GameObject text in degreeTextInstance){
            Destroy(text);
        }

        foreach(GameObject neighborIndicator in neighborIndicatorInstance){
            Destroy(neighborIndicator);
        }

    }

}

