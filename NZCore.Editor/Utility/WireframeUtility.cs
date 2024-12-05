// <copyright project="NZCore.Editor" file="WireframeUtility.cs" version="1.2.2">
// Copyright © 2024 Thomas Enzenebner. All rights reserved.
// </copyright>

using UnityEngine;

namespace NZCore.Editor
{
    public static class WireframeUtility
    {
        public static void DrawBox(Vector3 position, Vector3 boxSize)
        {
            GL.Begin(GL.TRIANGLES);
            GL.Color(Color.red);

            Vector3 halfSize = boxSize * 0.5f;

            // Define the vertices of the box
            Vector3[] vertices = new Vector3[]
            {
                position + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
                position + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
                position + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
                position + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
                position + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
                position + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
                position + new Vector3(halfSize.x, halfSize.y, halfSize.z),
                position + new Vector3(-halfSize.x, halfSize.y, halfSize.z),
            };

            // Define the triangles for each face
            int[,] triangles = new int[,]
            {
                // Bottom face
                { 0, 1, 2 }, { 0, 2, 3 },
                // Top face
                { 4, 6, 5 }, { 4, 7, 6 },
                // Front face
                { 0, 4, 5 }, { 0, 5, 1 },
                // Back face
                { 3, 2, 6 }, { 3, 6, 7 },
                // Left face
                { 0, 3, 7 }, { 0, 7, 4 },
                // Right face
                { 1, 5, 6 }, { 1, 6, 2 },
            };

            // Draw each triangle
            for (int i = 0; i < triangles.GetLength(0); i++)
            {
                GL.Vertex(vertices[triangles[i, 0]]);
                GL.Vertex(vertices[triangles[i, 1]]);
                GL.Vertex(vertices[triangles[i, 2]]);
            }

            GL.End();
        }

        public static void DrawWireframeCapsule(Vector3 position, Quaternion rotation, float radius, float height, int segments = 12)
        {
            DrawWireframeCylinder(position, rotation, radius, height - 2 * radius, segments);
            DrawHemisphere(position + Vector3.up * (height / 2 - radius), rotation, radius, segments);
            DrawHemisphere(position + Vector3.down * (height / 2 - radius), rotation * Quaternion.Euler(180, 0, 0), radius, segments);
        }
        
        public static void DrawWireframeCylinder(Vector3 position, Quaternion rotation, float radius, float height, int segments = 12)
        {
            float angleStep = 360f / segments;
            
            GL.Begin(GL.LINES);
            GL.Color(Color.green);
        
            for (int i = 0; i < segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                float nextAngle = angleStep * (i + 1) * Mathf.Deg2Rad;
        
                Vector3 point1 = new Vector3(Mathf.Cos(angle), -height, Mathf.Sin(angle)) * radius;
                Vector3 point2 = new Vector3(Mathf.Cos(nextAngle), -height, Mathf.Sin(nextAngle)) * radius;
                Vector3 point3 = new Vector3(Mathf.Cos(angle), height, Mathf.Sin(angle)) * radius;
                Vector3 point4 = new Vector3(Mathf.Cos(nextAngle), height, Mathf.Sin(nextAngle)) * radius;
        
                GL.Vertex(position + rotation * point1);
                GL.Vertex(position + rotation * point2);
                GL.Vertex(position + rotation * point3);
                GL.Vertex(position + rotation * point4);
        
                GL.Vertex(position + rotation * point1);
                GL.Vertex(position + rotation * point3);
            }
            
            GL.End();
        }
        
        public static void DrawHemisphere(Vector3 center, Quaternion rotation, float radius, int segments = 12)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.green);
    
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * Mathf.PI * 2 / segments;
                float angle2 = (i + 1) * Mathf.PI * 2 / segments;
        
                for (int j = 0; j < segments / 2; j++)
                {
                    float theta1 = j * (Mathf.PI) / (segments);  
                    float theta2 = (j + 1) * (Mathf.PI) / (segments);
            
                    Vector3 point1 = new Vector3(
                        Mathf.Cos(angle1) * Mathf.Sin(theta1),
                        Mathf.Cos(theta1),
                        Mathf.Sin(angle1) * Mathf.Sin(theta1)
                    ) * radius;
        
                    Vector3 point2 = new Vector3(
                        Mathf.Cos(angle2) * Mathf.Sin(theta1),
                        Mathf.Cos(theta1),
                        Mathf.Sin(angle2) * Mathf.Sin(theta1)
                    ) * radius;
        
                    Vector3 point3 = new Vector3(
                        Mathf.Cos(angle1) * Mathf.Sin(theta2),
                        Mathf.Cos(theta2),
                        Mathf.Sin(angle1) * Mathf.Sin(theta2)
                    ) * radius;
        
                    GL.Vertex(center + rotation * point1);
                    GL.Vertex(center + rotation * point2);
        
                    GL.Vertex(center + rotation * point1);
                    GL.Vertex(center + rotation * point3);
                }
            }
    
            GL.End();
        }
    }
}