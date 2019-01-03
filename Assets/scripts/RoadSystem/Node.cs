﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class Pair<T, U>
{
    public Pair()
    {
    }

    public Pair(T first, U second)
    {
        this.First = first;
        this.Second = second;
    }

    public T First { get; set; }
    public U Second { get; set; }
};

public class ConnectionInfo{
    public Pair<float, float> margins;
    public Pair<float, float> renderStart;
    public ConnectionInfo(){
        margins = new Pair<float, float>(0f, 0f);
        renderStart = new Pair<float, float>(0f, 0f);
    }
}

public class Node : MonoBehaviour
{
    /*<MarginRight (with respect to node), MarginRight>*/
    public List<Pair<Road, ConnectionInfo>> connection = new List<Pair<Road, ConnectionInfo>>();

    List<Curve> smoothPolygonEdges = new List<Curve>();

    public Vector3 position;

    public GameObject roadCornerIndicator;
    
    RoadDrawing indicatorInst;
    
    public float arcSmoothingRadius;

    public float bezeirSmoothingScale;

    GameObject smoothInstance;

    List<Vector3> debugPoints = new List<Vector3>();

    public void Awake()
    {
        indicatorInst = GameObject.FindWithTag("Road/curveIndicator").GetComponent<RoadDrawing>();
        position.y = Mathf.NegativeInfinity;
    }

    public Vector2 twodPosition{
        get{
            return new Vector2(position.x, position.z);
        }
    }

    public bool containsRoad(Road r){
        foreach(var rmPair in connection){
            if (rmPair.First == r){
                return true;
            }
        }
        return false;
    }

    public Pair<float, float> getMargin(Road r){
        /*get the margin with respect to road, (LeftMargin, RightMargin)*/
        for (int i = 0; i != connection.Count; ++i){
            if (connection[i].First == r){
                if (!startof(r.curve)){
                    return connection[i].Second.margins;
                }
                else{
                    return new Pair<float, float>(connection[i].Second.margins.Second, connection[i].Second.margins.First);
                }
            }
        }
        Debug.Assert(false);
        return new Pair<float, float>(0f, 0f);
    }

    public void addRoad(Road road)
    {
        Debug.Assert(position.y == Mathf.NegativeInfinity || Algebra.isclose(road.curve.at_ending(startof(road.curve)).y, position.y));

        connection.Add(new Pair<Road, ConnectionInfo>(road, new ConnectionInfo()));

        updateCrossroads();
    }

    public void removeRoad(Road road)
    {
        Pair<Road, ConnectionInfo> target = null;
        foreach (Pair<Road, ConnectionInfo> pair in connection){
            if (pair.First == road){
                target = pair;
                break;
            }
        }

        if (target != null)
        {
            connection.Remove(target);
        }
        else{
            Debug.Assert(false);
        }

        updateCrossroads();
        
    }

    //returns one directional line for each of the road connecting to this node.
    public List<Curve> directionalLines(float length, bool reverse = false){
        List<Curve> rtn = new List<Curve>();
        foreach(Pair<Road, ConnectionInfo> connect in connection){
            Vector2 direction = connect.First.curve.direction_ending_2d(startof(connect.First.curve));
            if (reverse){
                direction = -direction;
            }
            rtn.Add(new Line(twodPosition, twodPosition + direction * Algebra.InfLength, 0f, 0f));
        }
        return rtn;
    }

    public override int GetHashCode()
    {
        return position.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        Node n = obj as Node;
        return Algebra.isclose(n.position,  this.position);
    }

    void updateCrossroads(){
        Destroy(smoothInstance);
        smoothPolygonEdges.Clear();

        connection = connection.OrderBy(r =>
                                        startof(r.First.curve) ?
                                        r.First.curve.angle_ending(true) : r.First.curve.angle_ending(false)).ToList();

        foreach (var rmPair in connection){
            rmPair.Second = new ConnectionInfo();
        }

        if (connection.Count > 1)
        {
            smoothInstance = Instantiate(indicatorInst.roadIndicatorPrefab, transform);


            for (int i = 0; i != connection.Count; ++i)
            {
                int i_hat = (i == connection.Count - 1) ? 0 : i + 1;
                List<Curve> localSmootheners;
                var margins = smoothenCrossing(connection[i].First, connection[i_hat].First, out localSmootheners);
                if (smoothPolygonEdges.Count > 0 && localSmootheners.Count > 0)
                {
                    smoothPolygonEdges.Add(new Line(smoothPolygonEdges.Last().at_ending_2d(false), localSmootheners.First().at_ending_2d(true)));
                }
                smoothPolygonEdges.AddRange(localSmootheners);

                /*Add fence*/
                if (position.y > 0)
                {
                    generateNodeFence(localSmootheners);
                }

                connection[i].Second.margins.Second = margins.First;
                connection[i_hat].Second.margins.First = margins.Second;
            }

            if (smoothPolygonEdges.Count > 0)
            {
                Debug.Assert(smoothPolygonEdges.Count > 1);
                smoothPolygonEdges.Add(new Line(smoothPolygonEdges.Last().at_ending_2d(false), smoothPolygonEdges.First().at_ending_2d(true)));
                RoadRenderer smoothObjConfig = smoothInstance.GetComponent<RoadRenderer>();
                smoothObjConfig.generate(new Polygon(smoothPolygonEdges), position.y, "roadsurface");
            }

            foreach (var rmPair in connection)
            {
                Debug.Assert(rmPair.Second.margins.First >= 0 && rmPair.Second.margins.Second >= 0);
            }
        }

    }

    public bool startof(Curve c){
        return (c.at(0f) - position).magnitude < (c.at(1f) - position).magnitude;
    }

    Pair<float, float> smoothenCrossing(Road r1, Road r2, out List<Curve> smootheners)
    {
        float r1_angle = startof(r1.curve) ? r1.curve.angle_ending(true) : r1.curve.angle_ending(false);
        float r2_angle = startof(r2.curve) ? r2.curve.angle_ending(true) : r2.curve.angle_ending(false);
        float delta_angle = r1_angle < r2_angle ? r2_angle - r1_angle : r2_angle + 2 * Mathf.PI - r1_angle;
        this.r1 = r1;
        this.r2 = r2;
        smootheners = new List<Curve>();
        Vector2 streetCorner = approxStreetCorner();
        //debugPoints.Add(Algebra.toVector3(streetCorner));

        switch (Geometry.getAngleType(delta_angle)){
            case angleType.Sharp:
            case angleType.Blunt:
                if (c1_offset > 0f && c2_offset > 0f){
                    /*c1,c2>0*/
                    float extraSmoothingLength = arcSmoothingRadius / Mathf.Tan(delta_angle / 2);
                    smootheners.Add(new Arc(r1.curve.at_ending_2d(startof(r1.curve), c1_offset + extraSmoothingLength) +
                                            Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve), c1_offset + extraSmoothingLength) + Mathf.PI / 2) * r1.width / 2,
                                            Mathf.PI - delta_angle,
                                            r2.curve.at_ending_2d(startof(r2.curve), c2_offset + extraSmoothingLength) +
                                            Algebra.angle2dir(r2.curve.angle_ending(startof(r2.curve), c2_offset + extraSmoothingLength) - Mathf.PI / 2) * r2.width / 2));
                    return new Pair<float, float>(c1_offset + extraSmoothingLength, c2_offset + extraSmoothingLength);
                }
                if (c1_offset > 0f){
                    /*c1>0, c2<=0*/
                    float smoothRadius = -c2_offset;
                    smootheners.Add(new Arc(r1.curve.at_ending_2d(startof(r1.curve), c1_offset + smoothRadius) +
                                            Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve), c1_offset + smoothRadius) + Mathf.PI / 2) * r1.width / 2,
                                            Mathf.PI - delta_angle,
                                            r2.curve.at_ending_2d(startof(r2.curve)) +
                                            Algebra.angle2dir(r2.curve.angle_ending(startof(r2.curve)) - Mathf.PI / 2) * r2.width / 2));
                    /*TODO: calculate more precise delta_angle*/
                    return new Pair<float, float>(c1_offset + smoothRadius, 0);
                }
                if (c2_offset > 0f){
                    /*c1<0, c2>0*/
                    float smoothRadius = -c1_offset;
                    Curve smoothener = new Arc(r1.curve.at_ending_2d(startof(r1.curve)) +
                                               Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve)) + Mathf.PI / 2) * r1.width / 2,
                                               Mathf.PI - delta_angle,
                                               r2.curve.at_ending_2d(startof(r2.curve), c2_offset + smoothRadius) +
                                               Algebra.angle2dir(r2.curve.angle_ending(startof(r2.curve), c2_offset + smoothRadius) - Mathf.PI / 2) * r2.width / 2);
                    smootheners.Add(smoothener);
                    return new Pair<float, float>(0, c2_offset + smoothRadius);
                }
                Debug.Assert(false);
                break;
            case angleType.Flat:
                if (r1.width == r2.width){
                    return new Pair<float, float>(0, 0);
                }
                float widthDiff = Math.Abs(r1.width - r2.width) / 2;
                if (r1.width > r2.width){
                    Vector2 P0 = r1.curve.at_ending_2d(startof(r1.curve)) + 
                                   Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve)) + Mathf.PI / 2) * r1.width / 2;
                    Vector2 P1 = r2.curve.at_ending_2d(startof(r2.curve), widthDiff * bezeirSmoothingScale * 0.25f) +
                                   Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve), widthDiff * bezeirSmoothingScale * 0.25f) + Mathf.PI / 2) * r1.width / 2;
                    Vector2 P4 = r2.curve.at_ending_2d(startof(r2.curve) , widthDiff * bezeirSmoothingScale) +
                                   Algebra.angle2dir(r2.curve.angle_ending(startof(r2.curve), widthDiff * bezeirSmoothingScale) - Mathf.PI / 2) * r2.width / 2;
                    Vector2 P3 = r2.curve.at_ending_2d(startof(r2.curve), widthDiff * bezeirSmoothingScale * 0.75f) +
                                   Algebra.angle2dir(r2.curve.angle_ending(startof(r2.curve), widthDiff * bezeirSmoothingScale * 0.75f) - Mathf.PI / 2) * r2.width / 2;
                    Vector2 P2 = (P1 + P3) / 2;
                    smootheners.Add(new Bezeir(P0, P1, P2));
                    smootheners.Add(new Bezeir(P2, P3, P4));
                    return new Pair<float, float>(0f, widthDiff * bezeirSmoothingScale);
                }
                else{
                    Vector2 P0 = r1.curve.at_ending_2d(startof(r1.curve), widthDiff * bezeirSmoothingScale)
                                   + Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve), widthDiff * bezeirSmoothingScale) + Mathf.PI / 2) * r1.width / 2;
                    Vector2 P1 = r1.curve.at_ending_2d(startof(r1.curve), widthDiff * bezeirSmoothingScale * 0.75f)
                                   + Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve), widthDiff * bezeirSmoothingScale * 0.75f) + Mathf.PI / 2) * r1.width / 2;
                    Vector2 P3 = r1.curve.at_ending_2d(startof(r1.curve), widthDiff * bezeirSmoothingScale * 0.25f)
                                   + Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve), widthDiff * bezeirSmoothingScale * 0.25f) + Mathf.PI / 2) * r2.width / 2;
                    Vector2 P4 = r2.curve.at_ending_2d(startof(r2.curve)) +
                                   Algebra.angle2dir(r2.curve.angle_ending(startof(r2.curve)) - Mathf.PI / 2) * r2.width / 2;
                    Vector2 P2 = (P1 + P3) / 2;
                    smootheners.Add(new Bezeir(P0, P1, P2));
                    smootheners.Add(new Bezeir(P2, P3, P4));
                    return new Pair<float, float>(widthDiff * bezeirSmoothingScale, 0f);
                }
            case angleType.Reflex:
                float arcRadius = Mathf.Max(r1.width / 2, r2.width / 2);
                float bWidthDiff = Mathf.Abs(r1.width - r2.width) / 2;
                Curve arcSmoothener = new Arc(twodPosition,
                                        twodPosition + Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve)) + Mathf.PI / 2) * arcRadius,
                                        delta_angle - Mathf.PI);
                if (r1.width == r2.width){
                    smootheners.Add(arcSmoothener);
                    return new Pair<float, float>(0f, 0f);
                }
                if (r1.width > r2.width){
                    Vector2 P0 = r2.curve.at_ending_2d(startof(r2.curve)) + 
                                   Algebra.angle2dir(r2.curve.angle_ending(startof(r2.curve)) - Mathf.PI / 2) * r1.width / 2;
                    Vector2 P1 = r2.curve.at_ending_2d(startof(r2.curve), bWidthDiff * bezeirSmoothingScale * 0.25f) +
                                   Algebra.angle2dir(r2.curve.angle_ending(startof(r2.curve), bWidthDiff * bezeirSmoothingScale * 0.25f) - Mathf.PI / 2) * r1.width / 2;
                    Vector2 P4 = r2.curve.at_ending_2d(startof(r2.curve), bWidthDiff * bezeirSmoothingScale) 
                                   + Algebra.angle2dir(r2.curve.angle_ending(startof(r2.curve), bWidthDiff * bezeirSmoothingScale) - Mathf.PI / 2) * r2.width / 2;
                    Vector2 P3 = r2.curve.at_ending_2d(startof(r2.curve), bWidthDiff * bezeirSmoothingScale * 0.75f)
                                   + Algebra.angle2dir(r2.curve.angle_ending(startof(r2.curve), bWidthDiff * bezeirSmoothingScale * 0.75f) - Mathf.PI / 2) * r2.width / 2;
                    Vector2 P2 = (P1 + P3) / 2;
                    smootheners.Add(arcSmoothener);
                    smootheners.Add(new Bezeir(P0, P1, P2));
                    smootheners.Add(new Bezeir(P2, P3, P4));
                    return new Pair<float, float>(0f, bWidthDiff * bezeirSmoothingScale);
                }
                else{
                    Vector2 P0 = r1.curve.at_ending_2d(startof(r1.curve), bWidthDiff * bezeirSmoothingScale)
                                   + Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve), bWidthDiff * bezeirSmoothingScale) + Mathf.PI / 2) * r1.width / 2;
                    Vector2 P1 = r1.curve.at_ending_2d(startof(r1.curve), bWidthDiff * bezeirSmoothingScale * 0.75f)
                                   + Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve), bWidthDiff * bezeirSmoothingScale * 0.75f) + Mathf.PI / 2) * r1.width / 2;
                    Vector2 P3 = r1.curve.at_ending_2d(startof(r1.curve), bWidthDiff * bezeirSmoothingScale * 0.25f)
                                   + Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve), bWidthDiff * bezeirSmoothingScale * 0.25f) + Mathf.PI / 2) * r2.width / 2;
                    Vector2 P4 = r1.curve.at_ending_2d(startof(r1.curve))
                                   + Algebra.angle2dir(r1.curve.angle_ending(startof(r1.curve)) + Mathf.PI / 2) * r2.width / 2;
                    Vector2 P2 = (P1 + P3) / 2;
                    Debug.Log(P0 + " " + P1 + " " + P2 + " " + P3 + " " + P4);
                    smootheners.Add(new Bezeir(P0, P1, P2));
                    smootheners.Add(new Bezeir(P2, P3, P4));
                    smootheners.Add(arcSmoothener);
                    return new Pair<float, float>(bWidthDiff * bezeirSmoothingScale, 0f);
                }

            default:
                return new Pair<float, float>(-1f, -1f);
        }
        return new Pair<float, float>(-1f, -1f);

    }

    internal List<Vector2> getNeighborDirections(Vector2 direction)
    {

        if (connection.Count <= 2){
            return connection.ConvertAll((input) => input.First.curve.direction_ending_2d(startof(input.First.curve)));
        }

        List<float> anglesFromDirection =
            connection.ConvertAll((input) => Algebra.signedAngleToPositive(Vector2.SignedAngle(direction, input.First.curve.direction_ending_2d(startof(input.First.curve)))));

        return connection.FindAll((Pair<Road, ConnectionInfo> arg1) =>
                                  Algebra.signedAngleToPositive(Vector2.SignedAngle(direction, arg1.First.curve.direction_ending_2d(startof(arg1.First.curve)))) 
                                  == anglesFromDirection.Max() 
                                 ||
                                  Algebra.signedAngleToPositive(Vector2.SignedAngle(direction, arg1.First.curve.direction_ending_2d(startof(arg1.First.curve))))
                                  == anglesFromDirection.Min()).
                         ConvertAll((input) => input.First.curve.direction_ending_2d(startof(input.First.curve)));
    }

    /*
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(new Vector3(streetcorner.x, 2f, streetcorner.y), 0.1f);
    }
    */

    Road r1, r2;
    float c1_offset, c2_offset;

    float C1sidepointToC2Sidepoint(float l1)
    {
        float c2_tan_angle = r2.curve.angle_ending(startof(r2.curve), offset: c2_offset);
        Vector2 c2_normDir = new Vector2(Mathf.Cos(c2_tan_angle - Mathf.PI / 2), Mathf.Sin(c2_tan_angle - Mathf.PI / 2));
        float c1_tan_angle = r1.curve.angle_ending(startof(r1.curve), offset: l1);
        Vector2 c1_normDir = new Vector2(Mathf.Cos(c1_tan_angle + Mathf.PI / 2), Mathf.Sin(c1_tan_angle + Mathf.PI / 2));
        return ((r1.curve.at_ending_2d(startof(r1.curve), l1) + c1_normDir * r1.width / 2f) -
                (r2.curve.at_ending_2d(startof(r2.curve), c2_offset) + c2_normDir * r2.width / 2f)).magnitude;
    }

    float C2sidepointToC1Sidepoint(float l2)
    {
        float c2_tan_angle = r2.curve.angle_ending(startof(r2.curve), offset: l2);
        Vector2 c2_normDir = new Vector2(Mathf.Cos(c2_tan_angle - Mathf.PI / 2), Mathf.Sin(c2_tan_angle - Mathf.PI / 2));
        float c1_tan_angle = r1.curve.angle_ending(startof(r1.curve), offset: c1_offset);
        Vector2 c1_normDir = new Vector2(Mathf.Cos(c1_tan_angle + Mathf.PI / 2), Mathf.Sin(c1_tan_angle + Mathf.PI / 2));
        return ((r1.curve.at_ending_2d(startof(r1.curve), c1_offset) + c1_normDir * r1.width / 2f) -
                (r2.curve.at_ending_2d(startof(r2.curve), l2) + c2_normDir * r2.width / 2f)).magnitude;
    }

    Vector2 approxStreetCorner(){
        c1_offset = 0f;
        c2_offset = 0f;

        while (true){
            float c1_new_offset = Algebra.minArg(C1sidepointToC2Sidepoint, c1_offset);
            float c1_diff = Mathf.Abs(c1_offset - c1_new_offset);
            c1_offset = c1_new_offset;

            float c2_new_offset = Algebra.minArg(C2sidepointToC1Sidepoint, c2_offset);
            float c2_diff = Mathf.Abs(c2_offset - c2_new_offset);
            c2_offset = c2_new_offset;
            if (c1_diff + c2_diff < 1e-3)
                break;
        }
        float c2_tan_angle = r2.curve.angle_ending(startof(r2.curve), offset: c2_offset);
        Vector2 c2_normDir = new Vector2(Mathf.Cos(c2_tan_angle - Mathf.PI / 2), Mathf.Sin(c2_tan_angle - Mathf.PI / 2));

        return r2.curve.at_ending_2d(startof(r2.curve), c2_offset) + c2_normDir * r2.width / 2f;

    }

    void generateNodeFence(List<Curve> smoothener){
        foreach (Curve nodeSide in smoothener)
        {
            nodeSide.z_start = position.y;
            RoadRenderer smoothObjConfig = smoothInstance.GetComponent<RoadRenderer>();
            smoothObjConfig.generate(nodeSide, new List<String> { "singlefence" });
            nodeSide.z_start = 0f;
        }
    }

    /*End of modelling part; 
     * Begin of traffic part*/

    public Road getVirtualRoad(Road r1, Road r2){
        /*for now, do not consider direction arrangement*/
        Debug.Assert(containsRoad(r1) && containsRoad(r2));
        Debug.Assert(r1 != r2);
        float r1_margin = startof(r1.curve) ? r1.margin0End : r1.margin1End;
        float r2_margin = startof(r2.curve) ? r2.margin0End : r2.margin1End;
        Vector3 r1_endPos = r1.at(r1_margin);
        Vector3 r2_endPos = r2.at(r2_margin);

        Vector2 r1_direction = startof(r1.curve) ? - r1.curve.direction_2d(r1_margin) : r1.curve.direction_2d(r1_margin);
        Vector2 r2_direction = startof(r2.curve) ? - r2.curve.direction_2d(r2_margin) : r2.curve.direction_2d(r2_margin);

        if (Geometry.Parallel(r1_direction, r2_direction))
        {
            return new Road(new Line(r1_endPos, r2_endPos), r1.laneconfigure);
        }
        else
        {
            Line l1 = new Line(Algebra.toVector2(r1_endPos), Algebra.toVector2(r1_endPos) + Algebra.InfLength * r1_direction, r1_endPos.y, r1_endPos.y);
            Line l2 = new Line(Algebra.toVector2(r2_endPos), Algebra.toVector2(r2_endPos) + Algebra.InfLength * r2_direction, r2_endPos.y, r2_endPos.y);
            List<Vector3> intereSectionPoint = Geometry.curveIntersect(l1, l2);
            Debug.Assert(intereSectionPoint.Count == 1);
            return new Road(new Bezeir(r1_endPos, intereSectionPoint.First(), r2_endPos), r1.laneconfigure);
        }
    }
    /*
    private void OnDrawGizmos()
    {
        foreach(Vector3 p in debugPoints){
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(p, 0.2f);
        }
    }
    */
}
