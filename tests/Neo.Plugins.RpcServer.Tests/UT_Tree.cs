// Copyright (C) 2015-2026 The Neo Project.
//
// UT_Tree.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.RpcServer.Tests;

[TestClass]
public class UT_Tree
{
    [TestMethod]
    public void TestGetItemsReturnsDepthFirstItems()
    {
        Tree<string> tree = new();

        Assert.IsNull(tree.Root);
        Assert.IsEmpty(tree.GetItems());

        var root = tree.AddRoot("root");
        var left = root.AddChild("left");
        left.AddChild("left.child");
        root.AddChild("right");

        Assert.AreSame(root, tree.Root);
        Assert.AreEqual("root", root.Item);
        Assert.IsNull(root.Parent);
        Assert.AreSame(root, left.Parent);
        Assert.HasCount(2, root.Children);
        CollectionAssert.AreEqual(new[] { "root", "left", "left.child", "right" }, tree.GetItems().ToArray());
    }

    [TestMethod]
    public void TestAddRootRejectsSecondRoot()
    {
        Tree<int> tree = new();
        tree.AddRoot(1);

        Assert.ThrowsExactly<InvalidOperationException>(() => tree.AddRoot(2));
    }
}
