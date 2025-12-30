using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MeshesCombine
{
    public static class BinaryTreeUtil
    {
        public static void DrawGizmos(this BinaryTree tree, Color color)
        {
            if (tree == null || tree.Root == null)
            {
                Debug.Log("BinaryTreeUtil::DrawGizmos tree is empty");
                return;
            }

            DrawGizmos(tree.Root, color);
        }

        public static void DrawGizmos(BinaryTreeNode node, Color color)
        {
            if (node == null)
            {
                Debug.Log("BinaryTreeUtil::DrawGizmos node is empty");
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawWireCube(node.Center, node.Size);

            if (!node.HasChilds)
                return;

            DrawGizmos(node.Left, color);
            DrawGizmos(node.Right, color);
        }
    }
}
