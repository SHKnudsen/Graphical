﻿#region namespaces
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Graphical.Geometry;
using Graphical.Extensions;
using System.Diagnostics;
#endregion

namespace Graphical.Graphs
{
    /// <summary>
    /// Construction of VisibilityGraph Graph
    /// </summary>
    public class VisibilityGraph : Graph, ICloneable
    {
        #region Internal Properties

        internal Graph baseGraph { get; set; }

        #endregion

        #region Internal Constructors
        internal VisibilityGraph() : base()
        {
            baseGraph = new Graph();
        }

        public VisibilityGraph(Graph _baseGraph, bool reducedGraph, bool halfScan = true) : base()
        {
            baseGraph = _baseGraph;

            List<gEdge> resultEdges = VisibilityAnalysis(baseGraph, baseGraph.vertices, reducedGraph, halfScan);

            foreach (gEdge edge in resultEdges)
            {
                this.AddEdge(edge);
            }
        }
        #endregion

        #region Public Constructors

        public static List<Vertex> VertexVisibility(Vertex origin, Graph baseGraph)
        {
            Vertex o = origin;
            if(baseGraph.Contains(origin)) { o = baseGraph.vertices[baseGraph.vertices.IndexOf(origin)]; }
            var visibleVertices = VisibilityGraph.VisibleVertices(o, baseGraph, null, null, null, false, false, true);
            
            return visibleVertices;

        }

        // TODO: Review Merge method as now Ids are not duplicated per Graph
        public static VisibilityGraph Merge(List<VisibilityGraph> graphs)
        {
            Graph graph = new Graph();
            List<gEdge> edges = new List<gEdge>();
            foreach (VisibilityGraph g in graphs)
            {
                Dictionary<int, int> oldNewIds = new Dictionary<int, int>();
                foreach (gPolygon p in g.baseGraph.polygons.Values)
                {
                    int nextId = graph.GetNextId();
                    oldNewIds.Add(p.Id, nextId);
                    gPolygon polygon = (gPolygon)p.Clone();
                    polygon.Id = nextId;
                    graph.polygons.Add(nextId, polygon);
                }               

                foreach (gEdge e in g.edges)
                {
                    Vertex start = (Vertex)e.StartVertex.Clone();
                    Vertex end = (Vertex)e.EndVertex.Clone();
                    //start.polygonId = oldNewIds[start.polygonId];
                    //end.polygonId = oldNewIds[end.polygonId];
                    edges.Add(gEdge.ByStartVertexEndVertex(start, end));
                }
            }
            
            VisibilityGraph visibilityGraph = new VisibilityGraph()
            {
                baseGraph = new Graph(graph.polygons.Values.ToList()),
            };

            foreach(gEdge edge in edges)
            {
                visibilityGraph.AddEdge(edge);
            }

            return visibilityGraph;


        }
        #endregion

        #region Internal Methods

        internal List<gEdge> VisibilityAnalysis(Graph baseGraph, List<Vertex> vertices, bool reducedGraph, bool halfScan)
        {
            List<gEdge> visibleEdges = new List<gEdge>();

            foreach (Vertex v in vertices)
            {
                foreach (Vertex v2 in VisibleVertices(v, baseGraph, null, null, null, halfScan, reducedGraph))
                {
                    gEdge newEdge = new gEdge(v, v2);
                    if (!visibleEdges.Contains(newEdge)) { visibleEdges.Add(newEdge); }
                }
            }

            return visibleEdges;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="centre"></param>
        /// <param name="baseGraph"></param>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <param name="singleVertices"></param>
        /// <param name="scan"></param>
        /// <returns name="visibleVertices">List of vertices visible from the analysed vertex</returns>
        public static List<Vertex> VisibleVertices(
            Vertex centre,
            Graph baseGraph,
            Vertex origin = null,
            Vertex destination = null,
            List<Vertex> singleVertices = null,
            bool halfScan = true,
            bool reducedGraph = true,
            bool maxVisibility = false)
        {
            #region Initialize variables and sort vertices
            List<gEdge> edges = baseGraph.edges;
            List<Vertex> vertices = baseGraph.vertices;


            if (origin != null) { vertices.Add(origin); }
            if (destination != null) { vertices.Add(destination); }
            if (singleVertices != null) { vertices.AddRange(singleVertices); }


            Vertex maxVertex = vertices.OrderByDescending(v => v.DistanceTo(centre)).First();
            double maxDistance = centre.DistanceTo(maxVertex) * 1.5;
            //vertices = vertices.OrderBy(v => Point.RadAngle(centre.point, v.point)).ThenBy(v => centre.DistanceTo(v)).ToList();
            vertices = Vertex.OrderByRadianAndDistance(vertices, centre);

            #endregion

            #region Initialize openEdges
            //Initialize openEdges with any intersection edges on the half line 
            //from centre to maxDistance on the XAxis
            List<EdgeKey> openEdges = new List<EdgeKey>();
            double xMax = Math.Abs(centre.X) + 1.5 * maxDistance;
            gEdge halfEdge = gEdge.ByStartVertexEndVertex(centre, Vertex.ByCoordinates(xMax, centre.Y, centre.Z));
            foreach (gEdge e in edges)
            {
                if (centre.OnEdge(e)) { continue; }
                if (halfEdge.Intersects(e))
                {
                    if (e.StartVertex.OnEdge(halfEdge)) { continue; }
                    if (e.EndVertex.OnEdge(halfEdge)) { continue; }
                    EdgeKey k = new EdgeKey(halfEdge, e);
                    openEdges.AddItemSorted(k);
                }
            }

            #endregion

            List<Vertex> visibleVertices = new List<Vertex>();
            Vertex prev = null;
            bool prevVisible = false;
            for (var i = 0; i < vertices.Count; i++)
            {
                Vertex vertex = vertices[i];
                if (vertex.Equals(centre) || vertex.Equals(prev)) { continue; }// v == to centre or to previous when updating graph
                //Check only half of vertices as eventually they will become 'v'
                if (halfScan && Vertex.RadAngle(centre, vertex) > Math.PI) { break; }
                //Removing clock wise edges incident on v
                if (openEdges.Count > 0 && baseGraph.graph.ContainsKey(vertex))
                {
                    foreach (gEdge edge in baseGraph.graph[vertex])
                    {
                        int orientation = Vertex.Orientation(centre, vertex, edge.GetVertexPair(vertex));

                        if (orientation == -1)
                        {
                            EdgeKey k = new EdgeKey(centre, vertex, edge);
                            int index = openEdges.BisectIndex(k) - 1;
                            index = (index < 0) ? openEdges.Count - 1 : index;
                            if (openEdges.Count > 0 && openEdges.ElementAt(index).Equals(k))
                            {
                                openEdges.RemoveAt(index);
                            }
                        }
                    }
                }

                //Checking if p is visible from p.
                bool isVisible = false;
                gPolygon vertexPolygon = null;

                if (vertex.Parent is gPolygon) {
                    baseGraph.polygons.TryGetValue(vertex.Parent.Id, out vertexPolygon);
                }
                // If centre is on an edge of a inner polygon vertex belongs, check if the centre-vertex edge lies inside
                // or if on one of vertex's edges.
                if (vertexPolygon != null && !vertexPolygon.isBoundary && vertexPolygon.ContainsVertex(centre))
                {
                    Vertex mid = Vertex.MidVertex(centre, vertex);
                    // If mid is on any edge of vertex, is visible, otherwise not.
                    foreach(gEdge edge in baseGraph.graph[vertex])
                    {
                        if (mid.OnEdge(edge))
                        {
                            isVisible = true;
                            break;
                        }
                    }
                }
                //No collinear vertices
                else if (prev == null || Vertex.Orientation(centre, prev, vertex) != 0 || !prev.OnEdge(centre, vertex))
                {
                    
                    if (openEdges.Count == 0)
                    {
                        if (vertexPolygon != null && vertexPolygon.isBoundary && vertexPolygon.ContainsVertex(centre))
                        {
                            isVisible = vertexPolygon.ContainsVertex(Vertex.MidVertex(centre, vertex));
                        }
                        else
                        {
                            isVisible = true;
                        }
                    }
                    else if (vertex.OnEdge(openEdges.First().Edge) || !openEdges.First().Edge.Intersects(new gEdge(centre, vertex))) //TODO: Change this intersection to Edge.Intersects
                    {
                        isVisible = true;
                    }
                }
                //For collinear vertices, if previous was not visible, vertex is not either
                else if (!prevVisible)
                {
                    isVisible = false;
                }
                //For collinear vertices, if prev was visible need to check that
                //the edge from prev to vertex does not intersect with any open edge
                else
                {
                    isVisible = true;
                    foreach (EdgeKey k in openEdges)
                    {
                        //if (!k.edge.Contains(prev) && EdgeIntersect(prev, vertex, k.edge))
                        if (EdgeIntersect(prev, vertex, k.Edge) && !k.Edge.Contains(prev))
                        {
                            isVisible = false;
                            break;
                        }
                    }
                    // If visible (doesn't intersect any open edge) and edge 'prev-vertex'
                    // is in any polygon, vertex is visible if it belongs to a external boundary
                    if (isVisible && EdgeInPolygon(prev, vertex, baseGraph, maxDistance))
                    {
                        isVisible = IsBoundaryVertex(vertex, baseGraph);
                    }

                    // If still visible (not inside polygon or is boundary vertex),
                    // if not on 'centre-prev' edge means there is a gap between prev and vertex
                    if (isVisible && !vertex.OnEdge(centre, prev))
                    {
                        isVisible = !IsBoundaryVertex(vertex, baseGraph);
                    }
                }

                //If vertex is visible and centre belongs to any polygon, checks
                //if the visible edge is interior to its polygon
                if (isVisible && centre.Parent is gPolygon && !baseGraph.GetAdjecentVertices(centre).Contains(vertex))
                {
                    if (IsBoundaryVertex(centre, baseGraph) && IsBoundaryVertex(vertex, baseGraph))
                    {
                        isVisible = EdgeInPolygon(centre, vertex, baseGraph, maxDistance);
                    }
                    else
                    {
                        isVisible = !EdgeInPolygon(centre, vertex, baseGraph, maxDistance);
                    }
                }


                prev = vertex;
                prevVisible = isVisible;


                if (isVisible)
                {
                    // Check reducedGraph if vertices belongs to different polygons
                    // TODO: Implement IEquatable to all geometry types. Below might fail if no parent
                    if (reducedGraph && centre.Parent != vertex.Parent) 
                    {
                        bool isOriginExtreme =  true;
                        bool isTargetExtreme = true;
                        // For reduced graphs, it is checked if the edge is extrem or not.
                        // For an edge to be extreme, the edges coincident at the start and end vertex
                        // will have the same orientation (both clock or counter-clock wise)
                        
                        // Vertex belongs to a polygon
                        if (centre.Parent is gPolygon && !IsBoundaryVertex(centre, baseGraph))
                        {
                            var orientationsOrigin = baseGraph.GetAdjecentVertices(centre).Select(otherVertex => Vertex.Orientation(vertex, centre, otherVertex)).ToList();
                            isOriginExtreme = orientationsOrigin.All(o => o == orientationsOrigin.First());
                        }

                        if(centre.Parent is gPolygon && !IsBoundaryVertex(vertex, baseGraph))
                        {
                            var orientationsTarget = baseGraph.GetAdjecentVertices(vertex).Select(otherVertex => Vertex.Orientation(centre, vertex, otherVertex)).ToList();
                            isTargetExtreme = orientationsTarget.All(o => o == orientationsTarget.First());
                        }

                        if(isTargetExtreme || isOriginExtreme) { visibleVertices.Add(vertex); }
                    }
                    else
                    {
                        visibleVertices.Add(vertex);
                    }
                }

                if (baseGraph.Contains(vertex))
                {
                    foreach (gEdge e in baseGraph.graph[vertex])
                    {
                        if (!centre.OnEdge(e) && Vertex.Orientation(centre, vertex, e.GetVertexPair(vertex)) == 1)
                        {
                            EdgeKey k = new EdgeKey(centre, vertex, e);
                            openEdges.AddItemSorted(k);
                        }
                    }
                }

                if(isVisible && maxVisibility && vertex.Parent is gPolygon)
                {
                    List<Vertex> vertexPairs = baseGraph.GetAdjecentVertices(vertex);
                    int firstOrientation = Vertex.Orientation(centre, vertex, vertexPairs[0]);
                    int secondOrientation = Vertex.Orientation(centre, vertex, vertexPairs[1]);
                    bool isColinear = false;

                    //if both edges lie on the same side of the centre-vertex edge or one of them is colinear or centre is contained on any of the edges
                    if(firstOrientation == secondOrientation || firstOrientation == 0 || secondOrientation == 0)
                    {
                        Vertex rayVertex = vertex.Translate(gVector.ByTwoVertices(centre, vertex), maxDistance);
                        gEdge rayEdge = gEdge.ByStartVertexEndVertex(centre, rayVertex);
                        Vertex projectionVertex = null;

                        // if both orientation are not on the same side, means that one of them is colinear
                        isColinear = firstOrientation != secondOrientation ? true : false;

                        foreach(EdgeKey ek in openEdges)
                        {
                            Vertex intersection = rayEdge.Intersection(ek.Edge) as Vertex;
                            if(intersection != null &&!intersection.Equals(vertex))
                            {
                                projectionVertex = intersection;
                                gPolygon polygon;
                                
                                if(baseGraph.polygons.TryGetValue(vertex.Parent.Id, out polygon))
                                {
                                    // If polygon is internal, don't compute intersection if mid point lies inside the polygon but not on its edges
                                    Vertex mid = Vertex.MidVertex(vertex, intersection);
                                    bool containsEdge = Vertex.Orientation(centre, vertex, mid) != 0  && polygon.ContainsVertex(mid);
                                    if (!polygon.isBoundary && containsEdge)
                                    {
                                        projectionVertex = null;
                                    }
                                }
                                break;
                            }
                        }
                        if(projectionVertex != null)
                        {
                            // if edges are before rayEdge, projection Vertex goes after vertex
                            if(firstOrientation == -1 || secondOrientation == -1)
                            {
                                visibleVertices.Add(projectionVertex);
                            }
                            else
                            {
                                visibleVertices.Insert(visibleVertices.Count - 1, projectionVertex);
                            }
                        }
                    }
                    if(vertexPairs.Contains(centre) && !visibleVertices.Contains(centre))
                    {
                        visibleVertices.Add(centre);
                    }
                }
            }

            return visibleVertices;
        }

        internal static bool EdgeIntersect(gEdge halfEdge, gEdge edge)
        {
            //For simplicity, it only takes into acount the 2d projection to the xy plane,
            //so the result will be based on a porjection even if points have z values.
            bool intersects = EdgeIntersectProjection(
                halfEdge.StartVertex,
                halfEdge.EndVertex,
                edge.StartVertex,
                edge.EndVertex,
                "xy");

            return intersects;
        }

        internal static bool EdgeIntersect(Vertex start, Vertex end, gEdge edge)
        {
            //For simplicity, it only takes into acount the 2d projection to the xy plane,
            //so the result will be based on a porjection even if points have z values.
            bool intersects = EdgeIntersectProjection(
                start,
                end,
                edge.StartVertex,
                edge.EndVertex,
                "xy");

            return intersects;
        }

        internal static bool EdgeIntersectProjection(
            Vertex p1,
            Vertex q1,
            Vertex p2,
            Vertex q2,
            string plane = "xy")
        {
            //For more details https://www.geeksforgeeks.org/check-if-two-given-line-segments-intersect/

            int o1 = Vertex.Orientation(p1, q1, p2, plane);
            int o2 = Vertex.Orientation(p1, q1, q2, plane);
            int o3 = Vertex.Orientation(p2, q2, p1, plane);
            int o4 = Vertex.Orientation(p2, q2, q1, plane);

            //General case
            if (o1 != o2 && o3 != o4) { return true; }

            //Special Cases
            // p1, q1 and p2 are colinear and p2 lies on segment p1q1
            if (o1 == 0 && Vertex.OnEdgeProjection(p1, p2, q1, plane)) { return true; }

            // p1, q1 and p2 are colinear and q2 lies on segment p1q1
            if (o2 == 0 && Vertex.OnEdgeProjection(p1, q2, q1, plane)) { return true; }

            // p2, q2 and p1 are colinear and p1 lies on segment p2q2
            if (o3 == 0 && Vertex.OnEdgeProjection(p2, p1, q2, plane)) { return true; }

            // p2, q2 and q1 are colinear and q1 lies on segment p2q2
            if (o4 == 0 && Vertex.OnEdgeProjection(p2, q1, q2, plane)) { return true; }

            return false; //Doesn't fall on any of the above cases


        }

        internal static bool EdgeInPolygon(Vertex v1, Vertex v2, Graph graph, double maxDistance)
        {
            //Not on the same polygon
            if (v1.Parent.Id != v2.Parent.Id) { return false; }
            //At least one doesn't belong to any polygon
            if (!(v1.Parent is gPolygon) || !(v2.Parent is gPolygon) ) { return false; }
            Vertex midVertex = Vertex.MidVertex(v1, v2);
            return graph.polygons[v1.Parent.Id].ContainsVertex(midVertex);
        }

        internal static bool IsBoundaryVertex(Vertex vertex, Graph graph)
        {
            int polygonId = vertex.Parent.Id;
            return (polygonId < 0) ? false : graph.polygons[polygonId].isBoundary;
        }


        #endregion

        #region Public Methods
        /// <summary>
        /// Adds specific lines as gEdges to the visibility graph
        /// </summary>
        /// <param name="visibilityGraph">VisibilityGraph Graph</param>
        /// <param name="edges">Lines to add as new gEdges</param>
        /// <returns></returns>
        public static VisibilityGraph AddEdges(VisibilityGraph visibilityGraph, List<gEdge> edges)
        {
            //TODO: implement Dynamo' Trace 
            if (edges == null) { throw new NullReferenceException("edges"); }
            List<Vertex> singleVertices = new List<Vertex>();

            foreach (gEdge e in edges)
            {
                if (!singleVertices.Contains(e.StartVertex)) { singleVertices.Add(e.StartVertex); }
                if (!singleVertices.Contains(e.EndVertex)) { singleVertices.Add(e.EndVertex); }
            }
            VisibilityGraph updatedGraph = (VisibilityGraph)visibilityGraph.Clone();
            if (singleVertices.Any())
            {
                updatedGraph = AddVertices(visibilityGraph, singleVertices);

            }

            foreach (gEdge e in edges) { updatedGraph.AddEdge(e); }

            return updatedGraph;
        }

        /// <summary>
        /// Adds specific points as gVertices to the VisibilityGraph Graph
        /// </summary>
        /// <param name="visibilityGraph">VisibilityGraph Graph</param>
        /// <param name="vertices">Points to add as gVertices</param>
        /// <returns></returns>
        public static VisibilityGraph AddVertices(VisibilityGraph visibilityGraph, List<Vertex> vertices, bool reducedGraph = true)
        {
            //TODO: Seems that original graph gets updated as well
            if (vertices == null) { throw new NullReferenceException("vertices"); }

            VisibilityGraph newVisGraph = (VisibilityGraph)visibilityGraph.Clone();
            List<Vertex> singleVertices = new List<Vertex>();

            foreach (Vertex vertex in vertices)
            {
                if (newVisGraph.Contains(vertex)) { continue; }
                gEdge closestEdge = newVisGraph.baseGraph.edges.OrderBy(e => e.DistanceTo(vertex)).First();

                if (!closestEdge.DistanceTo(vertex).AlmostEqualTo(0))
                {
                    singleVertices.Add(vertex);
                }
                else if (vertex.OnEdge(closestEdge.StartVertex, closestEdge.EndVertex))
                {
                    int polygonId = closestEdge.StartVertex.Parent.Id;
                    newVisGraph.baseGraph.polygons[polygonId] = newVisGraph.baseGraph.polygons[polygonId].AddVertex(vertex, closestEdge);
                    singleVertices.Add(vertex);
                }
            }

            newVisGraph.baseGraph.ResetEdgesFromPolygons();

            foreach (Vertex centre in singleVertices)
            {
                foreach (Vertex v in VisibleVertices(centre, newVisGraph.baseGraph, null, null, singleVertices, false, reducedGraph))
                {
                    newVisGraph.AddEdge(new gEdge(centre, v));
                }
            }

            return newVisGraph;
        }

        public static Graph ShortestPath(VisibilityGraph visibilityGraph, Vertex origin, Vertex destination)
        {
            Graph shortest;

            bool containsOrigin = visibilityGraph.Contains(origin);
            bool containsDestination = visibilityGraph.Contains(destination);

            if (containsOrigin && containsDestination)
            {
                shortest = Algorithms.Dijkstra(visibilityGraph, origin, destination);
            }
            else
            {
                Vertex gO = (!containsOrigin) ? origin : null;
                Vertex gD = (!containsDestination) ? destination : null;
                Graph tempGraph = new Graph();

                if (!containsOrigin)
                {
                    foreach (Vertex v in VisibleVertices(origin, visibilityGraph.baseGraph, null, gD, null, false, true))
                    {
                        tempGraph.AddEdge(new gEdge(origin, v));
                    }
                }
                if (!containsDestination)
                {
                    foreach (Vertex v in VisibleVertices(destination, visibilityGraph.baseGraph, gO, null, null, false, true))
                    {
                        tempGraph.AddEdge(new gEdge(destination, v));
                    }
                }
                shortest = Algorithms.Dijkstra(visibilityGraph, origin, destination, tempGraph);
            }


            return shortest;
        }

        public List<double> ConnectivityFactor()
        {
            List<int> connected = new List<int>();
            foreach(gEdge edge in edges)
            {
                connected.Add(graph[edge.StartVertex].Count + graph[edge.EndVertex].Count());
            }
            int min = connected.Min();
            int max = connected.Max();

            return connected.Select(x => Convert.ToDouble(x).Map(min, max, 0, 1)).ToList();
        }

        #endregion



        public new object Clone()
        {
            VisibilityGraph newGraph = new VisibilityGraph()
            {
                graph = new Dictionary<Vertex, List<gEdge>>(),
                edges = new List<gEdge>(this.edges),
                polygons = new Dictionary<int, gPolygon>(this.polygons),
                baseGraph = (Graph)this.baseGraph.Clone()
            };

            foreach (var item in this.graph)
            {
                newGraph.graph.Add(item.Key, new List<gEdge>(item.Value));
            }

            return newGraph;
        }
        
    }

    /// <summary>
    /// VisibilityGraph graph's EdgeKey class to create a tree data structure.
    /// </summary>
    public class EdgeKey : IComparable<EdgeKey>
    {
        internal Vertex Centre { get; private set; }
        internal Vertex Vertex { get; private set; }
        internal gEdge Edge { get; private set; }
        internal gEdge RayEdge { get; private set; }

        internal EdgeKey(gEdge rayEdge, gEdge e)
        {
            RayEdge = rayEdge;
            Edge = e;
            Centre = RayEdge.StartVertex;
            Vertex = RayEdge.EndVertex;
        }
        
        internal EdgeKey(Vertex centre, Vertex end, gEdge e)
        {
            Centre = centre;
            Vertex = end;
            Edge = e;
            RayEdge = gEdge.ByStartVertexEndVertex(centre, end);
        }

        internal static double DistanceToIntersection(Vertex centre, Vertex maxVertex, gEdge e)
        {
            var centreProj = Vertex.ByCoordinates(centre.X, centre.Y, 0);
            var maxProj = Vertex.ByCoordinates(maxVertex.X, maxVertex.Y, 0);
            var startProj = Vertex.ByCoordinates(e.StartVertex.X, e.StartVertex.Y, 0);
            var endProj = Vertex.ByCoordinates(e.EndVertex.X, e.EndVertex.Y, 0);
            gEdge rayEdge = gEdge.ByStartVertexEndVertex(centreProj, maxProj);
            gEdge edgeProj = gEdge.ByStartVertexEndVertex(startProj, endProj);
            Geometry.Geometry intersection = rayEdge.Intersection(edgeProj);
            if(intersection != null && intersection.GetType() == typeof(Vertex))
            {
                return centre.DistanceTo((Vertex)intersection);
            }
            else
            {
                return 0;
            }
        }


        /// <summary>
        /// Override of Equals method
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }

            EdgeKey k = (EdgeKey)obj;
            return Edge.Equals(k.Edge);
        }

        /// <summary>
        /// Override of GetHashCode method
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Centre.GetHashCode() ^ Vertex.GetHashCode();
        }


        /// <summary>
        /// Implementation of IComparable interaface
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(EdgeKey other)
        {
            if (other == null) { return 1; }
            if (Edge.Equals(other.Edge)) { return 1; }
            if (!VisibilityGraph.EdgeIntersect(RayEdge, other.Edge)){ return -1; }

            double selfDist = DistanceToIntersection(Centre, Vertex, Edge);
            double otherDist = DistanceToIntersection(Centre, Vertex, other.Edge);

            if(selfDist > otherDist) { return 1; }
            else if(selfDist < otherDist) { return -1; }
            else
            {
                Vertex sameVertex = null;
                if (other.Edge.Contains(Edge.StartVertex)) { sameVertex = Edge.StartVertex; }
                else if (other.Edge.Contains(Edge.EndVertex)) { sameVertex = Edge.EndVertex; }
                double aslf = Vertex.ArcRadAngle( Vertex, Centre, Edge.GetVertexPair(sameVertex));
                double aot = Vertex.ArcRadAngle( Vertex, Centre, other.Edge.GetVertexPair(sameVertex));

                if(aslf < aot) { return -1; }
                else { return 1; }
            }

        }

        /// <summary>
        /// Implementaton of IComparable interface
        /// </summary>
        /// <param name="k1"></param>
        /// <param name="k2"></param>
        /// <returns></returns>
        public static bool operator <(EdgeKey k1, EdgeKey k2)
        {
            return k1.CompareTo(k2) < 0;
        }

        /// <summary>
        /// Implementation of IComparable interface
        /// </summary>
        /// <param name="k1"></param>
        /// <param name="k2"></param>
        /// <returns></returns>
        public static bool operator >(EdgeKey k1, EdgeKey k2)
        {
            return k1.CompareTo(k2) > 0;
        }

        /// <summary>
        /// Override of ToString method.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("EdgeKey: (gEdge={0}, centre={1}, vertex={2})", Edge.ToString(), Centre.ToString(), Vertex.ToString());
        }
    }
}