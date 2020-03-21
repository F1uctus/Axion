using Axion.Core.Processing.CodeGen;
using Axion.Core.Processing.Syntactic.Expressions.Atomic;
using Axion.Core.Processing.Syntactic.Expressions.TypeNames;

namespace Axion.Core.Processing.Syntactic.Expressions.Definitions {
    public class NameDef : Expr, IDefinitionExpr, IDecorableExpr {
        private NameExpr name;

        public NameExpr Name {
            get => name;
            set => SetNode(ref name, value);
        }

        private Expr value;

        public Expr Value {
            get => value;
            set => SetNode(ref this.value, value);
        }

        private TypeName valueType;

        public sealed override TypeName ValueType {
            get => valueType /* BUG here ?? Right.ValueType*/;
            protected set => SetNode(ref valueType, value);
        }

        public NameDef(
            Expr?     parent = null,
            NameExpr? name   = null,
            TypeName? type   = null,
            Expr?     value  = null
        ) : base(
            parent
         ?? GetParentFromChildren(name, type, value)
        ) {
            Name      = name;
            ValueType = type;
            Value     = value;
        }

        public override void ToAxion(CodeWriter c) {
            c.Write(Name);
            if (ValueType != null) {
                c.Write(": ", ValueType);
            }

            if (Value != null) {
                c.Write(" = ", Value);
            }
        }

        public override void ToCSharp(CodeWriter c) {
            if (Value == null) {
                c.Write(ValueType, " ", Name);
            }
            else {
                c.Write(
                    (object) ValueType ?? "var", " ", Name, " = ",
                    Value
                );
            }
        }

        public override void ToPython(CodeWriter c) {
            c.Write(Name);
            if (ValueType != null) {
                c.Write(": ", ValueType);
            }

            if (Value != null) {
                c.Write(" = ", Value);
            }
        }
    }
}