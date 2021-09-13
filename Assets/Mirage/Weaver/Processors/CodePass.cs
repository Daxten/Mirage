
using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace Mirage.Weaver
{
    /// <summary>
    /// Algorithm for doing a pass over the code
    /// </summary>
    public static class CodePass
    {
        /// <summary>
        /// Process an instruction,  it can replace the instruction and return new instruction
        /// else it should return the same instruction
        /// </summary>
        /// <param name="md">The method containing the instruction</param>
        /// <param name="instruction">The instruction being processed</param>
        /// <returns>return the same instruction, or replace the instruction and return the replacement</returns>
        public delegate Instruction InstructionProcessor(MethodDefinition md, Instruction instruction, SequencePoint sequencePoint);

        internal static List<MethodDefinition> GetAllMethodBodies(ModuleDefinition module)
        {
            // guess 5 methods per type as starting capacity
            var methods = new List<MethodDefinition>(module.Types.Count * 5);
            foreach (TypeDefinition td in module.Types)
            {
                GetMethodsInType(methods, td);
            }
            return methods;
        }
        static void GetMethodsInType(List<MethodDefinition> methods, TypeDefinition td)
        {
            if (!td.IsClass) { return; }

            foreach (MethodDefinition md in td.Methods)
            {
                methods.Add(md);
            }

            foreach (TypeDefinition nested in td.NestedTypes)
            {
                GetMethodsInType(methods, nested);
            }
        }

        /// <summary>
        /// Executes a method for every instruction in a module
        /// </summary>
        /// <param name="module">The module to be passed over</param>
        /// <param name="selector">A predicate that indicates if we should pass over a method or not</param>
        /// <param name="processor">The function that processes each instruction</param>
        public static void ForEachInstruction(ModuleDefinition module, Predicate<MethodDefinition> selector, InstructionProcessor processor)
        {
            var types = new List<TypeDefinition>(module.Types);

            foreach (TypeDefinition td in types)
            {
                if (td.IsClass)
                {
                    InstructionPass(td, selector, processor);
                }
            }
        }

        public static void ForEachInstruction(ModuleDefinition module, InstructionProcessor processor) =>
            ForEachInstruction(module, md => true, processor);

        private static void InstructionPass(TypeDefinition td, Predicate<MethodDefinition> selector, InstructionProcessor processor)
        {
            foreach (MethodDefinition md in td.Methods)
            {
                InstructionPass(md, selector, processor);
            }

            foreach (TypeDefinition nested in td.NestedTypes)
            {
                InstructionPass(nested, selector, processor);
            }
        }

        private static void InstructionPass(MethodDefinition md, Predicate<MethodDefinition> selector, InstructionProcessor processor)
        {
            // process all references to replaced members with properties
            if (md.IsAbstract || md.Body == null || md.Body.Instructions == null)
            {
                return;
            }

            if (md.Body.CodeSize <= 0 || !selector(md))
            {
                return;
            }

            Collection<SequencePoint> sequencePoints = md.DebugInformation.SequencePoints;

            int sequencePointIndex = 0;
            Instruction instr = md.Body.Instructions[0];

            while (instr != null)
            {
                SequencePoint sequencePoint;
                (sequencePoint, sequencePointIndex) = GetSequencePoint(sequencePoints, sequencePointIndex, instr);
                instr = processor(md, instr, sequencePoint);
                instr = instr.Next;
            }
        }

        // I need the sequence point for an instructions,  but the mapping is odd,  and MethodDebugInformation.GetSequencePoint
        // only maps exact locations.
        // this gets executed for every instruction in an assembly, so it must be efficient
        public static (SequencePoint sequencePoint, int sequencePointIndex) GetSequencePoint(Collection<SequencePoint> sequencePoints, int index, Instruction instr)
        {
            if (sequencePoints.Count == 0)
            {
                return (null, 0);
            }

            SequencePoint sequencePoint = sequencePoints[index];

            if (index + 1 >= sequencePoints.Count)
                return (sequencePoint, index);

            SequencePoint next = sequencePoints[index + 1];

            if (next.Offset > instr.Offset)
                return (sequencePoint, index);

            return (next, index + 1);
        }

        public static void GetSequencePointFast(Collection<SequencePoint> sequencePoints, ref int index, Instruction instr)
        {
            // dont increment if it would be above limit
            if (index + 1 >= sequencePoints.Count)
                return;

            SequencePoint nextSQ = sequencePoints[index + 1];

            // dont increment if nextSQ will be above instruction
            if (nextSQ.Offset > instr.Offset)
                return;

            index++;
        }
        public static SequencePoint GetSequencePointForInstructiion(Collection<SequencePoint> sequencePoints, Instruction instr)
        {
            int offset = instr.Offset;
            for (int i = 0; i < sequencePoints.Count; i++)
            {
                SequencePoint sequencePoint = sequencePoints[i];
                if (sequencePoint.Offset == offset)
                {
                    return sequencePoint;
                }
            }
            return null;
        }
    }
}
