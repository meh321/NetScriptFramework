using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetScriptFramework
{
    /// <summary>
    /// Base implementation of a type that has a virtual table.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IVirtualObject" />
    /// <seealso cref="NetScriptFramework.MemoryObject" />
    public abstract class VirtualObject : MemoryObject, IVirtualObject
    {
        #region Members

        /// <summary>
        /// Invokes a "thiscall" native function from the virtual table of this object.
        /// </summary>
        /// <param name="offset">The offset of function in the virtual table.</param>
        /// <param name="args">The arguments of function.</param>
        /// <returns></returns>
        public IntPtr InvokeVTableThisCall<T>(int offset, params InvokeArgument[] args) where T : IVirtualObject
        {
            var self = this.Cast<T>();
            if (self == IntPtr.Zero)
                throw new InvalidCastException("Unable to cast object to " + typeof(T).Name + "!");

            var vtable = this.VTable<T>();
            if (vtable == IntPtr.Zero)
                throw new NullReferenceException("Virtual function table was null!");

            vtable += offset;
            var funcAddr = Memory.ReadPointer(vtable);
            return Memory.InvokeThisCall(self, funcAddr, args);
        }

        /// <summary>
        /// Invokes a "thiscall" native function that returns a floating point value from the virtual table of this object.
        /// </summary>
        /// <param name="offset">The offset of function in the virtual table.</param>
        /// <param name="args">The arguments of function.</param>
        /// <returns></returns>
        public float InvokeVTableThisCallF<T>(int offset, params InvokeArgument[] args) where T : IVirtualObject
        {
            var self = this.Cast<T>();
            if (self == IntPtr.Zero)
                throw new InvalidCastException("Unable to cast object to " + typeof(T).Name + "!");

            var vtable = this.VTable<T>();
            if (vtable == IntPtr.Zero)
                throw new NullReferenceException("Virtual function table was null!");

            vtable += offset;
            var funcAddr = Memory.ReadPointer(vtable);
            return Memory.InvokeThisCallF(self, funcAddr, args);
        }

        /// <summary>
        /// Invokes a "thiscall" native function that returns a floating point value from the virtual table of this object.
        /// </summary>
        /// <param name="offset">The offset of function in the virtual table.</param>
        /// <param name="args">The arguments of function.</param>
        /// <returns></returns>
        public double InvokeVTableThisCallD<T>(int offset, params InvokeArgument[] args) where T : IVirtualObject
        {
            var self = this.Cast<T>();
            if (self == IntPtr.Zero)
                throw new InvalidCastException("Unable to cast object to " + typeof(T).Name + "!");

            var vtable = this.VTable<T>();
            if (vtable == IntPtr.Zero)
                throw new NullReferenceException("Virtual function table was null!");

            vtable += offset;
            var funcAddr = Memory.ReadPointer(vtable);
            return Memory.InvokeThisCallD(self, funcAddr, args);
        }

        /// <summary>
        /// Gets an object from memory of an unknown type. Returns null if unable to identify or not a valid object. The returned object may be invalid because it only checks virtual function table address!
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">Game library is not loaded! Unable to use types.</exception>
        public static IVirtualObject FromAddress(IntPtr address)
        {
            var game = Main.Game;
            if (game == null)
                throw new ArgumentException("Game library is not loaded! Unable to use types.");

            if (address != IntPtr.Zero)
            {
                TypeDescriptor td = null;

                // Not using "TryRead" on purpose because bad pointer should cause exception instead of returning null!
                var ptr = Memory.ReadPointer(address);
                if (Main.Game.Types.TypesByVTable.TryGetValue(ptr, out td))
                {
                    var mo = td.Creator();
                    mo.Address = address - td.OffsetInFullType;
                    if(mo is VirtualObject)
                        return (VirtualObject)mo;
                }

                return null;
            }

            return null;
        }

        #endregion
    }

    #region IVirtualObject interface

    /// <summary>
    /// Base implementation of a type that has a virtual table.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IMemoryObject" />
    public interface IVirtualObject : IMemoryObject
    {
        /// <summary>
        /// Invokes a "thiscall" native function from the virtual table of this object.
        /// </summary>
        /// <param name="offset">The offset of function in the virtual table. This is not the index!</param>
        /// <param name="args">The arguments of function.</param>
        /// <returns></returns>
        IntPtr InvokeVTableThisCall<T>(int offset, params InvokeArgument[] args) where T : IVirtualObject;

        /// <summary>
        /// Invokes a "thiscall" native function that returns a floating point value from the virtual table of this object.
        /// </summary>
        /// <param name="offset">The offset of function in the virtual table. This is not the index!</param>
        /// <param name="args">The arguments of function.</param>
        /// <returns></returns>
        float InvokeVTableThisCallF<T>(int offset, params InvokeArgument[] args) where T : IVirtualObject;

        /// <summary>
        /// Invokes a "thiscall" native function that returns a floating point value from the virtual table of this object.
        /// </summary>
        /// <param name="offset">The offset of function in the virtual table. This is not the index!</param>
        /// <param name="args">The arguments of function.</param>
        /// <returns></returns>
        double InvokeVTableThisCallD<T>(int offset, params InvokeArgument[] args) where T : IVirtualObject;
    }

    #endregion
}
