using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Axion.Core.Processing.Lexical.Tokens;
using Axion.Core.Processing.Syntactic;
using Axion.Core.Processing.Syntactic.Expressions;
using Axion.Core.Processing.Syntactic.Expressions.Atomic;
using Axion.Core.Processing.Syntactic.Expressions.Definitions;
using Axion.Core.Processing.Syntactic.Expressions.Generic;
using Axion.Core.Processing.Syntactic.Expressions.Operations;
using Axion.Core.Processing.Syntactic.Expressions.Postfix;
using Axion.Core.Processing.Syntactic.Expressions.Statements;
using Axion.Core.Processing.Syntactic.Expressions.TypeNames;

namespace Axion.Core.Processing.Traversal {
    public class NoTraversePathAttribute : Attribute { }

    public static class Traversing {
        /// <summary>
        ///     Applies (if possible) traversing/reducing function
        ///     to each child node of specified root expression.
        /// </summary>
        public static void Traverse(Expr root) {
            // TODO: fix reducing of macros
            if (root is MacroApplicationExpr) {
                return;
            }
            if (!root.Path.Traversed) {
                Walker(root.Path);
            }

            root = root.Path.Node;
            PropertyInfo[] exprProps = root.GetType().GetProperties();
            IEnumerable<PropertyInfo> childProps = exprProps.Where(
                p => typeof(Expr).IsAssignableFrom(p.PropertyType)
                  && !Attribute.IsDefined(p, typeof(NoTraversePathAttribute), false)
                  || p.PropertyType.IsGenericType
                  && p.PropertyType
                      .GetInterfaces()
                      .Where(i => i.IsGenericType)
                      .Select(i => i.GetGenericTypeDefinition())
                      .Contains(typeof(IList<>))
                  && typeof(Span).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0])
            );
            foreach (PropertyInfo prop in childProps) {
                object obj = prop.GetValue(root);
                if (obj == null) {
                    continue;
                }

                if (obj is Expr expr) {
                    Traverse(expr);
                }
                else {
                    try {
                        List<Span> list = ((IEnumerable) obj).OfType<Span>().ToList();
                        // for loop required, expressions collection
                        // can be modified.
                        // ReSharper disable once ForCanBeConvertedToForeach
                        for (var i = 0; i < list.Count; i++) {
                            Traverse((Expr) list[i]);
                        }
                    }
                    catch (InvalidCastException) {
                        // ignored
                    }
                }
            }
        }

        /// <summary>
        ///     Default traversing/reducing function for Axion source code.
        ///     Simplifies some specific syntax to generic representation.
        ///     (e.g. resolves pipelines, unwraps class data-members, no-break loop, etc.)
        /// </summary>
        public static void Walker(ITreePath path) {
            switch (path.Node) {
            case TupleTypeName t when t.Types.Count == 0: {
                path.Node      = new SimpleTypeName("UnitType");
                path.Traversed = true;
                break;
            }

            case UnionTypeName unionTypeName: {
                // `LeftType | RightType` -> `Union[LeftType, RightType]`
                path.Node = new GenericTypeName(
                    path.Node.Parent,
                    new SimpleTypeName("Union"),
                    new NodeList<TypeName>(path.Node) { unionTypeName.Left, unionTypeName.Right }
                );
                path.Traversed = true;
                break;
            }

            case BinaryExpr bin when bin.Operator.Is(TokenType.OpIs)
                                  && bin.Right is UnaryExpr un
                                  && un.Operator.Is(TokenType.OpNot): {
                // `x is (not (y))` -> `not (x is y)`
                path.Node = new UnaryExpr(
                    path.Node.Parent,
                    TokenType.OpNot,
                    new BinaryExpr(
                        path.Node,
                        bin.Left,
                        new OperatorToken(path.Node.Source, tokenType: TokenType.OpIs),
                        un.Value
                    )
                );
                path.Traversed = true;
                break;
            }

            case BinaryExpr bin when bin.Operator.Is(TokenType.RightPipeline): {
                // `arg |> func` -> `func(arg)`
                path.Node = new FuncCallExpr(
                    path.Node.Parent,
                    bin.Right,
                    new FuncCallArg(path.Node.Parent, value: bin.Left)
                );
                path.Traversed = true;
                break;
            }

            case BinaryExpr bin when bin.Operator.Is(TokenType.OpAssign)
                                  && bin.Left.GetType().IsGenericType
                                  && bin.Left.GetType().GetGenericTypeDefinition() == typeof(TupleExpr<>): {
                // (x, y) = GetCoordinates()
                // <=======================>
                // unwrappedX = GetCoordinates()
                // x = unwrappedX.x
                // y = unwrappedX.y
                var scope = bin.GetParentOfType<ScopeExpr>();
                (_, int deconstructionIdx) = scope.IndexOf(bin);
                var deconstructionVar = new VarDef(
                    scope,
                    new NameExpr(scope.CreateUniqueId("unwrapped{0}")),
                    value: bin.Right,
                    immutable: true
                );
                scope.Items[deconstructionIdx] = deconstructionVar;
                var tpl = (TupleExpr<Expr>) bin.Left;
                for (var i = 0; i < tpl.Expressions.Count; i++) {
                    scope.Items.Insert(
                        deconstructionIdx + i + 1,
                        new VarDef(
                            scope,
                            (NameExpr) tpl.Expressions[i],
                            value: new MemberAccessExpr(scope, deconstructionVar.Name) {
                                Member = tpl.Expressions[i]
                            }
                        )
                    );
                }

                path.Traversed = true;
                break;
            }

            case WhileExpr whileExpr when whileExpr.NoBreakScope != null: {
                // Add bool before loop, that indicates, was break reached or not.
                // Find all 'break'-s in child scopes and set this
                // bool to 'true' before exiting the loop.
                // Example:
                // while x
                //     do()
                //     if a
                //         do2()
                //         break
                // nobreak
                //     do3()
                // <============================>
                // loop-X-nobreak = true
                // while x
                //     do()
                //     if a
                //         do2()
                //         loop-X-nobreak = false
                //         break
                // if loop-X-nobreak
                //     do3()
                var scope = path.Node.GetParentOfType<ScopeExpr>();
                (_, int whileIndex) = scope.IndexOf(whileExpr);
                var flagName = new NameExpr(scope.CreateUniqueId("loop_{0}_nobreak"));
                scope.Items.Insert(
                    whileIndex,
                    new VarDef(
                        path.Node,
                        flagName,
                        value: new ConstantExpr(path.Node, "true")
                    )
                );
                // index of while == whileIdx + 1
                List<(BreakExpr item, ScopeExpr itemParentScope, int itemIndex)> breaks =
                    whileExpr.Scope.FindItemsOfType<BreakExpr>();
                var boolSetter = new BinaryExpr(
                    path.Node,
                    flagName,
                    new OperatorToken(path.Node.Source, tokenType: TokenType.OpAssign),
                    new ConstantExpr(path.Node, "false")
                );
                foreach ((_, ScopeExpr itemParentScope, int itemIndex) in breaks) {
                    itemParentScope.Items.Insert(itemIndex, boolSetter);
                }

                scope.Items.Insert(
                    whileIndex + 2,
                    new IfExpr(path.Node, flagName, whileExpr.NoBreakScope)
                );
                whileExpr.NoBreakScope = null;
                path.Traversed         = true;
                break;
            }

            case ClassDef cls when cls.DataMembers.Count > 0: {
                // class Point (x: int, y: int)
                //     fn print
                //         print(x, y)
                // <============================>
                // class Point
                //     x: int
                //     y: int
                //     fn print
                //         print(x, y)
                foreach (Expr dataMember in cls.DataMembers) {
                    cls.Scope.Items.Insert(0, dataMember);
                }
                cls.DataMembers.Clear();
                path.Traversed = true;
                break;
            }
            }
        }
    }
}