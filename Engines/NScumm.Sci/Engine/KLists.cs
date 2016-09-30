//  Author:
//       scemino <scemino74@gmail.com>
//
//  Copyright (c) 2015 
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using static NScumm.Core.DebugHelper;

namespace NScumm.Sci.Engine
{
    internal class sort_temp_t
    {
        public Register key, value;
        public Register order;
    }

    internal partial class Kernel
    {
        private static Register kEmptyList(EngineState s, int argc, StackPtr argv)
        {
            if (argv[0].IsNull)
                return Register.NULL_REG;

            List list = s._segMan.LookupList(argv[0]);
# if CHECK_LISTS
            checkListPointer(s._segMan, argv[0]);
#endif
            return Register.Make(0, ((list != null) ? list.first.IsNull : false));
        }

        private static Register kNewList(EngineState s, int argc, StackPtr argv)
        {
            Register listRef;
            var list = s._segMan.AllocateList(out listRef);
            list.first = list.last = Register.NULL_REG;
            DebugC(DebugLevels.Nodes, "New listRef at {0}", listRef);

            return listRef; // Return list base address
        }

        private static Register kFindKey(EngineState s, int argc, StackPtr argv)
        {
            Register node_pos;
            Register key = argv[1];
            Register list_pos = argv[0];

            //Debug($"Looking for key {key} in list {list_pos}");

# if CHECK_LISTS
            checkListPointer(s._segMan, argv[0]);
#endif

            node_pos = s._segMan.LookupList(list_pos).first;

            //Debug($"First node at {node_pos}");

            while (!node_pos.IsNull)
            {
                Node n = s._segMan.LookupNode(node_pos);
                if (n.key == key)
                {
                    // Debug($" Found key at {node_pos}");
                    return node_pos;
                }

                node_pos = n.succ;
                //Debug($"NextNode at {node_pos}");
            }

            //Debug($"Looking for key without success");
            return Register.NULL_REG;
        }

        private static Register kNewNode(EngineState s, int argc, StackPtr argv)
        {
            Register nodeValue = argv[0];
            // Some SCI32 games call this with 1 parameter (e.g. the demo of Phantasmagoria).
            // Set the key to be the same as the value in this case
            Register nodeKey = (argc == 2) ? argv[1] : argv[0];
            s.r_acc = s._segMan.NewNode(nodeValue, nodeKey);

            DebugC(DebugLevels.Nodes, $"New nodeRef at {s.r_acc}");

            return s.r_acc;
        }

        private static Register kAddAfter(EngineState s, int argc, StackPtr argv)
        {
            List list = s._segMan.LookupList(argv[0]);
            Node firstnode = argv[1].IsNull ? null : s._segMan.LookupNode(argv[1]);
            Node newnode = s._segMan.LookupNode(argv[2]);

# if CHECK_LISTS
            checkListPointer(s._segMan, argv[0]);
#endif

            if (newnode == null)
            {
                throw new InvalidOperationException($"New 'node' {argv[2]} is not a node");
            }

            if (argc != 3 && argc != 4)
            {
                throw new InvalidOperationException("kAddAfter: Haven't got 3 or 4 arguments, aborting");
            }

            if (argc == 4)
                newnode.key = argv[3];

            if (firstnode != null)
            { // We're really appending after
                Register oldnext = firstnode.succ;

                newnode.pred = argv[1];
                firstnode.succ = argv[2];
                newnode.succ = oldnext;

                if (oldnext.IsNull)  // Appended after last node?
                                     // Set new node as last list node
                    list.last = argv[2];
                else
                    s._segMan.LookupNode(oldnext).pred = argv[2];

            }
            else { // !firstnode
                AddToFront(s, argv[0], argv[2]); // Set as initial list node
            }

            return s.r_acc;
        }

        private static Register kAddToEnd(EngineState s, int argc, StackPtr argv)
        {
            AddToEnd(s, argv[0], argv[1]);

            if (argc == 3)
                s._segMan.LookupNode(argv[1]).key = argv[2];

            return s.r_acc;
        }

        private static Register kAddToFront(EngineState s, int argc, StackPtr argv)
        {
            AddToFront(s, argv[0], argv[1]);

            if (argc == 3)
                s._segMan.LookupNode(argv[1]).key = argv[2];

            return s.r_acc;
        }

        private static Register kFirstNode(EngineState s, int argc, StackPtr argv)
        {
            if (argv[0].IsNull)
                return Register.NULL_REG;

            List list = s._segMan.LookupList(argv[0]);

            if (list != null)
            {
# if CHECK_LISTS
                checkListPointer(s._segMan, argv[0]);
#endif
                return list.first;
            }
            else {
                return Register.NULL_REG;
            }
        }

        private static Register kLastNode(EngineState s, int argc, StackPtr argv)
        {
            if (argv[0].IsNull)
                return Register.NULL_REG;

            List list = s._segMan.LookupList(argv[0]);

            if (list != null)
            {
# if CHECK_LISTS
                checkListPointer(s._segMan, argv[0]);
#endif
                return list.last;
            }
            else {
                return Register.NULL_REG;
            }
        }

        private static Register kDisposeList(EngineState s, int argc, StackPtr argv)
        {
            // This function is not needed in ScummVM. The garbage collector
            // cleans up unused objects automatically

            return s.r_acc;
        }

        private static Register kNextNode(EngineState s, int argc, StackPtr argv)
        {
            Node n = s._segMan.LookupNode(argv[0]);

# if CHECK_LISTS
            if (!isSaneNodePointer(s._segMan, argv[0]))
                return NULL_REG;
#endif

            return n.succ;
        }

        private static Register kPrevNode(EngineState s, int argc, StackPtr argv)
        {
            Node n = s._segMan.LookupNode(argv[0]);

# if CHECK_LISTS
            if (!isSaneNodePointer(s._segMan, argv[0]))
                return NULL_REG;
#endif

            return n.pred;
        }

        private static Register kNodeValue(EngineState s, int argc, StackPtr argv)
        {
            Node n = s._segMan.LookupNode(argv[0]);

# if CHECK_LISTS
            if (!isSaneNodePointer(s._segMan, argv[0]))
                return NULL_REG;
#endif

            // ICEMAN: when plotting a course in room 40, unDrawLast is called by
            // startPlot::changeState, but there is no previous entry, so we get 0 here
            return n != null ? n.value : Register.NULL_REG;
        }

        private static Register kDeleteKey(EngineState s, int argc, StackPtr argv)
        {
            Register node_pos = kFindKey(s, 2, argv);
            List list = s._segMan.LookupList(argv[0]);

            if (node_pos.IsNull)
                return Register.NULL_REG; // Signal failure

            var n = s._segMan.LookupNode(node_pos);
            if (list.first == node_pos)
                list.first = n.succ;
            if (list.last == node_pos)
                list.last = n.pred;

            if (!n.pred.IsNull)
                s._segMan.LookupNode(n.pred).succ = n.succ;
            if (!n.succ.IsNull)
                s._segMan.LookupNode(n.succ).pred = n.pred;

            // Erase references to the predecessor and successor nodes, as the game
            // scripts could reference the node itself again.
            // Happens in the intro of QFG1 and in Longbow, when exiting the cave.
            n.pred = Register.NULL_REG;
            n.succ = Register.NULL_REG;

            return Register.Make(0, 1); // Signal success
        }

        private static Register kSort(EngineState s, int argc, StackPtr argv)
        {
            SegManager segMan = s._segMan;
            Register source = argv[0];
            Register dest = argv[1];
            Register order_func = argv[2];

            int input_size = (short)SciEngine.ReadSelectorValue(segMan, source, o => o.size);
            Register input_data = SciEngine.ReadSelector(segMan, source, o => o.elements);
            Register output_data = SciEngine.ReadSelector(segMan, dest, o => o.elements);

            List list;
            Node node;

            if (input_size == 0)
                return s.r_acc;

            if (output_data.IsNull)
            {
                list = s._segMan.AllocateList(out output_data);
                list.first = list.last = Register.NULL_REG;
                SciEngine.WriteSelector(segMan, dest, o => o.elements, output_data);
            }

            SciEngine.WriteSelectorValue(segMan, dest, o => o.size, (ushort)input_size);

            list = s._segMan.LookupList(input_data);
            node = s._segMan.LookupNode(list.first);

            var temp_array = new List<sort_temp_t>();

            int i = 0;
            while (node != null)
            {
                Register[] @params = { node.value };

                SciEngine.InvokeSelector(s, order_func, o => o.doit, argc, argv, 1, new StackPtr(@params, 0));
                temp_array[i].key = node.key;
                temp_array[i].value = node.value;
                temp_array[i].order = s.r_acc;
                i++;
                node = s._segMan.LookupNode(node.succ);
            }

            temp_array.Sort(sort_temp_cmp);

            for (i = 0; i < input_size; i++)
            {
                Register lNode = s._segMan.NewNode(temp_array[i].value, temp_array[i].key);

                AddToEnd(s, output_data, lNode);
            }

            return s.r_acc;
        }

        private static int sort_temp_cmp(sort_temp_t st1, sort_temp_t st2)
        {
            if (st1.order.Segment < st2.order.Segment ||
                (st1.order.Segment == st2.order.Segment &&
                st1.order.Offset < st2.order.Offset))
                return -1;

            if (st1.order.Segment > st2.order.Segment ||
                (st1.order.Segment == st2.order.Segment &&
                st1.order.Offset > st2.order.Offset))
                return 1;

            return 0;
        }

        private static void AddToFront(EngineState s, Register listRef, Register nodeRef)
        {
            List list = s._segMan.LookupList(listRef);
            Node newNode = s._segMan.LookupNode(nodeRef);

            DebugC(DebugLevels.Nodes, "Adding node {0} to end of list {1}", nodeRef, listRef);

            if (newNode == null)
                throw new InvalidOperationException($"Attempt to add non-node ({nodeRef}) to list at {listRef}");

# if CHECK_LISTS
            checkListPointer(s._segMan, listRef);
#endif

            newNode.pred = Register.NULL_REG;
            newNode.succ = list.first;

            // Set node to be the first and last node if it's the only node of the list
            if (list.first.IsNull)
                list.last = nodeRef;
            else {
                Node oldNode = s._segMan.LookupNode(list.first);
                oldNode.pred = nodeRef;
            }
            list.first = nodeRef;
        }

        private static void AddToEnd(EngineState s, Register listRef, Register nodeRef)
        {
            List list = s._segMan.LookupList(listRef);
            Node newNode = s._segMan.LookupNode(nodeRef);

            DebugC(DebugLevels.Nodes, "Adding node {0} to end of list {1}", nodeRef, listRef);

            if (newNode == null)
                throw new InvalidOperationException($"Attempt to add non-node ({nodeRef}) to list at {listRef}");

# if CHECK_LISTS
            checkListPointer(s._segMan, listRef);
#endif

            newNode.pred = list.last;
            newNode.succ = Register.NULL_REG;

            // Set node to be the first and last node if it's the only node of the list
            if (list.last.IsNull)
                list.first = nodeRef;
            else {
                Node old_n = s._segMan.LookupNode(list.last);
                old_n.succ = nodeRef;
            }
            list.last = nodeRef;
        }
    }
}
