﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
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

class Node : MonoBehaviour
{
    public List<Pair<Road, float>> connection = new List<Pair<Road, float>>();

    public Vector3 position;

    public GameObject roadCornerIndicator;

    RoadDrawing indicatorInst;

    public float crossingSmoothScale;

    List<GameObject> smoothInstances = new List<GameObject>();

    public void Awake()
    {
        indicatorInst = GameObject.FindWithTag("Road/curveIndicator").GetComponent<RoadDrawing>();
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

    public float getEndMargin(Road r){
        for (int i = 0; i != connection.Count; ++i){
            if (connection[i].First == r){
                return connection[i].Second;
            }
        }
        Debug.Assert(false);
        return 0f;
    }

    public void addRoad(Road road)
    {
        connection.Add(new Pair<Road, float>(road, 0f));

        updateCrossroads();
    }

    public void removeRoad(Road road)
    {
        Pair<Road, float> target = null;
        foreach (Pair<Road, float> pair in connection){
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
        if (connection.Count > 0)
        {
            updateCrossroads();
        }
    }


    public override int GetHashCode()
    {
        return position.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        Node n = obj as Node;
        return n.position == this.position;
    }

    void updateCrossroads(){
        foreach (GameObject smoothinst in smoothInstances){
            Destroy(smoothinst);
        }
        smoothInstances.Clear();

        connection = connection.OrderBy(r =>
                                        startof(r.First.curve) ?
                                        r.First.curve.angle_ending(true) : r.First.curve.angle_ending(false)).ToList();
        foreach (var rmPair in connection){
            rmPair.Second = 0f;
        }

        if (connection.Count > 1)
        {
            for (int i = 0; i != connection.Count; ++i)
            {
                if (i == connection.Count - 1)
                {
                    var margins = smoothenCrossing(connection[i].First, connection[0].First);
                    connection[i].Second = Mathf.Max(connection[i].Second, margins.First);
                    connection[0].Second = Mathf.Max(connection[0].Second, margins.Second);

                }
                else
                {
                    var margins = smoothenCrossing(connection[i].First, connection[i + 1].First);
                    connection[i].Second = Mathf.Max(connection[i].Second, margins.First);
                    connection[i + 1].Second = Mathf.Max(connection[i + 1].Second, margins.Second);
                }
            }

        }
    }
    /*
    float minCrossingRadius(Road r1, Road r2){
        float r1_angle = startof(r1.curve) ? r1.curve.angle_ending(true) : r1.curve.angle_ending(false);
        float r2_angle = startof(r2.curve) ? r2.curve.angle_ending(true) : r2.curve.angle_ending(false);
        float delta_angle = r1_angle - r2_angle;
        if (Algebra.isclose(Mathf.Sin(delta_angle), 0f)){
            return 0f;
        }
        else{
            float w1 = r2.width / 2;
            float w2 = r2.width / 2;
            return Mathf.Sqrt(w1 * w1 / (Mathf.Sin(delta_angle) * Mathf.Sin(delta_angle)) +
                              2 * w1 * w2 * Mathf.Cos(delta_angle) / (Mathf.Sin(delta_angle) * Mathf.Sin(delta_angle))
                              + w2 * w2 / (Mathf.Sin(delta_angle) * Mathf.Sin(delta_angle)));
        }
    }
    */
    bool startof(Curve c){
        return (c.at(0f) - position).magnitude < (c.at(1f) - position).magnitude;
    }

    Pair<float, float> smoothenCrossing(Road r1, Road r2)
    {
        float r1_angle = startof(r1.curve) ? r1.curve.angle_ending(true) : r1.curve.angle_ending(false);
        float r2_angle = startof(r2.curve) ? r2.curve.angle_ending(true) : r2.curve.angle_ending(false);
        float delta_angle = r1_angle < r2_angle ? r2_angle - r1_angle : r2_angle + 2 * Mathf.PI - r1_angle;
        if (Algebra.isclose(0f, delta_angle) || delta_angle >= Mathf.PI)
        {
            return new Pair<float, float>(0f, 0f);
        }
        if (Algebra.isclose(Mathf.Sin(r1_angle - r2_angle), 0f))
            return new Pair<float, float>(0f, 0f);


        //float r1_roadcornerOffsetApprox = (r2.width / 2 + Mathf.Cos(delta_angle) * r1.width / 2) / (Mathf.Sin(delta_angle));
        //float r2_roadcornerOffsetApprox = (r1.width / 2 + Mathf.Cos(delta_angle) * r2.width / 2) / (Mathf.Sin(delta_angle));


        this.r1 = r1;
        this.r2 = r2;
        Vector2 streetcorner = approxStreetCorner();
        float smoothLength = Mathf.Min((streetcorner - twodPosition).magnitude * this.crossingSmoothScale, 2 * Mathf.Min(r1.width, r2.width));

        float r1_curveIntersectAngle = r1.curve.angle_ending(startof(r1.curve), c1_offset);
        float r2_curveIntersectAngle = r2.curve.angle_ending(startof(r2.curve), c2_offset);
        Vector2 r1_curveIntersect = r1.curve.at_ending(startof(r1.curve), c1_offset + smoothLength) +
                                      new Vector2(Mathf.Cos(r1_curveIntersectAngle + Mathf.PI / 2), Mathf.Sin(r1_curveIntersectAngle + Mathf.PI / 2)) * r1.width / 2;
        Vector2 r2_curveIntersect = r2.curve.at_ending(startof(r2.curve), c2_offset + smoothLength) +
                                      new Vector2(Mathf.Cos(r2_curveIntersectAngle - Mathf.PI / 2), Mathf.Sin(r2_curveIntersectAngle - Mathf.PI / 2)) * r2.width / 2;
        float roadcornerHalfAngle = 0.5f * (r1_curveIntersectAngle < r2_curveIntersectAngle ? r2_curveIntersectAngle - r1_curveIntersectAngle :
                                     r2_curveIntersectAngle + 2 * Mathf.PI - r1_curveIntersectAngle);
        Vector2 smoothArcCenter = streetcorner + (r1_curveIntersect + r2_curveIntersect - 2 * streetcorner).normalized * smoothLength / Mathf.Cos(roadcornerHalfAngle);
        float smoothInnerRadius = smoothLength * Mathf.Tan(roadcornerHalfAngle);
        float smoothOuterRadius = smoothLength / Mathf.Cos(roadcornerHalfAngle);

        Road narrowerRoad = r1.width > r2.width ? r2 : r1;
        if (smoothOuterRadius <= smoothLength * Mathf.Tan(roadcornerHalfAngle) + narrowerRoad.width){
            Arc ac_without_width = new Arc(r1_curveIntersect, Mathf.PI - 2 * roadcornerHalfAngle, r2_curveIntersect, 0f, 0f);
            float smoothwidth = (ac_without_width.center - streetcorner).magnitude - ac_without_width.radius;

            Vector2 r1_curveIntersect_pluswidth = r1_curveIntersect + new Vector2(Mathf.Cos(r1_curveIntersectAngle - Mathf.PI / 2), Mathf.Sin(r1_curveIntersectAngle - Mathf.PI / 2)) * smoothwidth / 2;
            Vector2 r2_curveIntersect_pluswidth = r2_curveIntersect + new Vector2(Mathf.Cos(r2_curveIntersectAngle + Mathf.PI / 2), Mathf.Sin(r2_curveIntersectAngle + Mathf.PI / 2)) * smoothwidth / 2;

            GameObject smoothObj = Instantiate(indicatorInst.roadIndicatorPrefab, transform);
            smoothInstances.Add(smoothObj);
            RoadRenderer smoothObjConfig = smoothObj.GetComponent<RoadRenderer>();
            smoothObjConfig.generate(new Arc(r1_curveIntersect_pluswidth, Mathf.PI - 2 * roadcornerHalfAngle, r2_curveIntersect_pluswidth, 0f, 0f),
                                     new List<string> { string.Format("surface_{0}", smoothwidth) });
        }
        else{
            Arc curve1, curve2;
            float smoothWidth1, smoothWidth2;
            if (narrowerRoad == r1){
                smoothWidth1 = r1.width;
                smoothWidth2 = smoothOuterRadius - smoothInnerRadius;
                float angle1 = Mathf.Acos((smoothInnerRadius + r1.width) / smoothOuterRadius);
                curve1 = new Arc(smoothArcCenter, r1_curveIntersect + (r1_curveIntersect - smoothArcCenter).normalized * smoothWidth1 / 2, - angle1, 0f, 0f);
                curve2 = new Arc(smoothArcCenter, r2_curveIntersect + (r2_curveIntersect - smoothArcCenter).normalized * smoothWidth2 / 2, (Mathf.PI -  2 * roadcornerHalfAngle - angle1), 0f, 0f);
            }
            else{
                smoothWidth1 = smoothOuterRadius - smoothInnerRadius;
                smoothWidth2 = r2.width;
                float angle2 = Mathf.Acos((smoothInnerRadius + r2.width) / smoothOuterRadius);
                curve1 = new Arc(smoothArcCenter, r1_curveIntersect + (r1_curveIntersect - smoothArcCenter).normalized * smoothWidth1 / 2, angle2, 0f, 0f);
                curve2 = new Arc(smoothArcCenter, r2_curveIntersect + (r2_curveIntersect - smoothArcCenter).normalized * smoothWidth2 / 2, -(Mathf.PI - 2 * roadcornerHalfAngle - angle2), 0f, 0f);
            }
            GameObject smoothObj = Instantiate(indicatorInst.roadIndicatorPrefab, transform);
            smoothInstances.Add(smoothObj);
            RoadRenderer smoothObjConfig = smoothObj.GetComponent<RoadRenderer>();
            smoothObjConfig.generate(curve1, new List<string> { string.Format("surface_{0}", smoothWidth1) });
            smoothObjConfig.generate(curve2, new List<string> { string.Format("surface_{0}", smoothWidth2) });
        }
        return new Pair<float, float>(c1_offset + smoothLength, c2_offset + smoothLength);
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
        return ((r1.curve.at_ending(startof(r1.curve), l1) + c1_normDir * r1.width / 2f) -
                (r2.curve.at_ending(startof(r2.curve), c2_offset) + c2_normDir * r2.width / 2f)).magnitude;
    }

    float C2sidepointToC1Sidepoint(float l2)
    {
        float c2_tan_angle = r2.curve.angle_ending(startof(r2.curve), offset: l2);
        Vector2 c2_normDir = new Vector2(Mathf.Cos(c2_tan_angle - Mathf.PI / 2), Mathf.Sin(c2_tan_angle - Mathf.PI / 2));
        float c1_tan_angle = r1.curve.angle_ending(startof(r1.curve), offset: c1_offset);
        Vector2 c1_normDir = new Vector2(Mathf.Cos(c1_tan_angle + Mathf.PI / 2), Mathf.Sin(c1_tan_angle + Mathf.PI / 2));
        return ((r1.curve.at_ending(startof(r1.curve), c1_offset) + c1_normDir * r1.width / 2f) -
                (r2.curve.at_ending(startof(r2.curve), l2) + c2_normDir * r2.width / 2f)).magnitude;
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

        return r2.curve.at_ending(startof(r2.curve), c2_offset) + c2_normDir * r2.width / 2f;

    }
}
