#if !NO_RUNTIME
using System;

using ProtoBuf.Meta;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif



namespace ProtoBuf.Serializers
{
    sealed class FieldDecorator : ProtoDecoratorBase
    {

        public override Type ExpectedType { get { return forType; } }
        private readonly FieldInfo field;
        private readonly Type forType;
        private readonly Type overrideType;
        public override bool RequiresOldValue { get { return true; } }
        public override bool ReturnsValue { get { return false; } }
        public FieldDecorator(Type forType, Type overrideType, FieldInfo field, IProtoSerializer tail) : base(tail)
        {
            Helpers.DebugAssert(forType != null);
            Helpers.DebugAssert(field != null);
            this.forType = forType;
            this.overrideType = overrideType;
            this.field = field;
        }
#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            Helpers.DebugAssert(value != null);
            value = field.GetValue(value);
            if(value != null) Tail.Write(value, dest);
        }
        public override object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value != null);
            object newValue = Tail.Read((Tail.RequiresOldValue ? field.GetValue(value) : null), source);
            if(newValue != null) field.SetValue(value,newValue);
            return null;
        }
#endif

#if FEAT_COMPILER
        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.LoadAddress(valueFrom, ExpectedType);
            ctx.LoadValue(field);
            ctx.WriteNullCheckedTail(field.FieldType, Tail, null, overrideType);
        }
        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (Compiler.Local loc = ctx.GetLocalWithValue(ExpectedType, valueFrom))
            {
                if (Tail.RequiresOldValue)
                {
                    ctx.LoadAddress(loc, ExpectedType);
                    ctx.LoadValue(field);  
                }
                // value is either now on the stack or not needed
                ctx.ReadNullCheckedTail(field.FieldType, Tail, null, overrideType);

                if (Tail.ReturnsValue)
                {
                    var newType = overrideType != null ? overrideType : field.FieldType;
                    using (Compiler.Local newVal = new Compiler.Local(ctx, newType))
                    {
                        ctx.StoreValue(newVal);
                        if (Helpers.IsValueType(field.FieldType))
                        {
                            ctx.LoadAddress(loc, ExpectedType);
                            ctx.LoadValue(newVal);
                            ctx.StoreValue(field);
                        }
                        else
                        {
                            Compiler.CodeLabel allDone = ctx.DefineLabel();
                            ctx.LoadValue(newVal);
                            ctx.BranchIfFalse(allDone, true); // interpret null as "don't assign"

                            ctx.LoadAddress(loc, ExpectedType);
                            ctx.LoadValue(newVal);
                            if (overrideType != null)
                            {
                                ctx.Cast(field.FieldType);
                            }
                            ctx.StoreValue(field);

                            ctx.MarkLabel(allDone);
                        }
                    }
                }
            }
        }
#endif
    }
}
#endif