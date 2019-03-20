﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Graphical.Graphs;
using Graphical.Geometry;
using Graphical.DataStructures;

namespace Graphical
{
    public static class Algorithms
    {
        
        public static Graph Dijkstra(Graph graph, Vertex origin, Vertex destination, Graph tempGraph = null)
        {
            MinPriorityQ<Vertex, double> Q = new MinPriorityQ<Vertex, double>();
            bool originInGraph = false;

            foreach(Vertex v in graph.vertices)
            {
                if (v.Equals(origin))
                {
                    originInGraph = true;
                    Q.Add(origin, 0);
                }
                else
                {
                    Q.Add(v, Double.PositiveInfinity);
                }
            }

            //If tempGraph is not null, means graph doesn't contain origin and/or destination vertices.
            if (!originInGraph)
            {
                Q.Add(origin, 0);
            }

            if (!graph.Contains(destination)) { Q.Add(destination, Double.PositiveInfinity); }

            Dictionary<Vertex, Vertex> ParentVertices = new Dictionary<Vertex, Vertex>();
            List<Vertex> S = new List<Vertex>();

            while (Q.Size > 0)
            {
                double minDistance = Q.PeekValue();
                Vertex vertex = Q.Take();
                S.Add(vertex);

                if (vertex.Equals(destination)) { break; }

                List<gEdge> edges = new List<gEdge>();
                edges.AddRange(graph.GetVertexEdges(vertex));
                if(tempGraph != null && tempGraph.edges.Any())
                {
                    edges.AddRange(tempGraph.GetVertexEdges(vertex));
                }

                foreach(gEdge e in edges)
                {
                    Vertex w = e.GetVertexPair(vertex);
                    double newLength = minDistance + e.Length;
                    
                    if(!S.Contains(w) && newLength < Q.GetValue(w))
                    {
                        Q.UpdateItem(w, newLength);
                        //dist[w] = newLength;
                        ParentVertices[w] = vertex;
                    }
                }

            }

            Graph path = new Graph();
            Vertex dest = destination;
            while (dest != origin)
            {
                Vertex parent = ParentVertices[dest];
                path.AddEdge(new gEdge(parent, dest));
                dest = parent;
            }
            // Reversing edges list so they will be sorted from origin to target
            path.edges.Reverse();
            return path;
            
        }
        
    }
}
