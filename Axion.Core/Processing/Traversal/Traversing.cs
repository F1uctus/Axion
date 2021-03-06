using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Axion.Core.Processing.Lexical.Tokens;
using Axion.Core.Processing.Syntactic.Expressions;
using Axion.Core.Processing.Syntactic.Expressions.Atomic;
using Axion.Core.Processing.Syntactic.Expressions.Definitions;
using Axion.Core.Processing.Syntactic.Expressions.Operations;
using Axion.Core.Processing.Syntactic.Expressions.Patterns;
using Axion.Core.Processing.Syntactic.Expressions.Postfix;
using Axion.Core.Processing.Syntactic.Expressions.Statements;
using Axion.Core.Processing.Syntactic.Expressions.TypeNames;
using static Axion.Specification.TokenType;

namespace Axion.Core.Processing.Traversal {
    public class NoPathTraversingAttribute : Attribute { }

    public static class Traversing {
        /// <summary>
        ///     Applies (if possible) traversing/reducing function
        ///     to each child node of specified root expression.
        /// </summary>
        public static void Traverse(Node root) {
            // TODO: fix reducing of macros
            if (root is MacroMatchExpr or Pattern or Token) {
                return;
            }

            if (!root.Path.Traversed) {
                Walker(root.Path);
            }

            root = root.Path.Node;
            var nodeProps = root.GetType().GetProperties();
            var childProps = nodeProps.Where(p =>
                typeof(Node).IsAssignableFrom(p.PropertyType)
             && !Attribute.IsDefined(
                    p,
                    typeof(NoPathTraversingAttribute),
                    true
                )
             || p.PropertyType.IsGenericType
             && p.PropertyType.GetInterfaces()
                    .Where(i => i.IsGenericType)
                    .Select(i => i.GetGenericTypeDefinition())
                    .Contains(typeof(IList<>))
             && typeof(Node).IsAssignableFrom(
                    p.PropertyType.GetGenericArguments()[0]
                )
            );
            foreach (var prop in childProps) {
                var obj = prop.GetValue(root);
                switch (obj) {
                case null: continue;
                case Node n:
                    Traverse(n);
                    break;
                default:
                    try {
                        var list = ((IEnumerable) obj).OfType<Node>()
                            .ToArray();
                        // for loop required, expressions collection
                        // can be modified.
                        // ReSharper disable once ForCanBeConvertedToForeach
                        for (var i = 0; i < list.Length; i++) {
                            Traverse(list[i]);
                        }
                    }
                    catch (InvalidCastException) {
                        // ignored
                    }

                    break;
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
            case UnionTypeName unionTypeName: {
                // `LeftType | RightType` -> `Union[LeftType, RightType]`
                path.Node = new GenericTypeName(path.Node.Parent) {
                    Target = new SimpleTypeName(path.Node, "Union"),
                    TypeArgs = {
                        unionTypeName.Left,
                        unionTypeName.Right
                    }
                };
                path.Traversed = true;
                break;
            }

            case BinaryExpr bin when bin.Operator.Is(Is)
                                  && bin.Right is UnaryExpr un
                                  && un.Operator.Is(Not): {
                // `x is (not (y))` -> `not (x is y)`
                path.Node = new UnaryExpr(path.Node.Parent) {
                    Operator = new OperatorToken(
                        path.Node.Unit,
                        tokenType: Not
                    ),
                    Value = new BinaryExpr(path.Node) {
                        Left = bin.Left,
                        Operator = new OperatorToken(
                            path.Node.Unit,
                            tokenType: Is
                        ),
                        Right = un.Value
                    }
                };
                path.Traversed = true;
                break;
            }

            case BinaryExpr bin
                when bin.Operator.Is(PipeRightAngle): {
                // `arg |> func` -> `func(arg)`
                path.Node = new FuncCallExpr(path.Node.Parent) {
                    Target = bin.Right,
                    Args = {
                        new FuncCallArg(path.Node.Parent) {
                            Value = bin.Left
                        }
                    }
                };
                path.Traversed = true;
                break;
            }

            case BinaryExpr bin when bin.Operator.Is(EqualsSign)
                                  && bin.Left is TupleExpr tpl: {
                // (x, y) = GetCoordinates()
                // <=======================>
                // unwrappedX = GetCoordinates()
                // x = unwrappedX.x
                // y = unwrappedX.y
                var scope = bin.GetParent<ScopeExpr>();
                var (_, deconstructionIdx) = scope!.IndexOf(bin);
                var deconstructionVar = new VarDef(
                    scope,
                    new Token(bin.Unit, KeywordLet)
                ) {
                    Name = new NameExpr(
                        bin,
                        scope.CreateUniqueId("unwrapped{0}")
                    ),
                    Value = bin.Right
                };
                scope.Items[deconstructionIdx] = deconstructionVar;
                for (var i = 0; i < tpl.Expressions.Count; i++) {
                    scope.Items.Insert(
                        deconstructionIdx + i + 1,
                        new VarDef(scope) {
                            Name = (NameExpr) tpl.Expressions[i],
                            Value = new MemberAccessExpr(scope) {
                                Target = deconstructionVar.Name,
                                Member = tpl.Expressions[i]
                            }
                        }
                    );
                }

                path.Traversed = true;
                break;
            }

            case WhileExpr { NoBreakScope: { } } whileExpr: {
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
                var scope = path.Node.GetParent<ScopeExpr>();
                var (_, whileIndex) = scope.IndexOf(whileExpr);
                var flagName = new NameExpr(
                    scope,
                    scope.CreateUniqueId("loop_{0}_nobreak")
                );
                scope.Items.Insert(
                    whileIndex,
                    new VarDef(path.Node) {
                        Name  = flagName,
                        Value = ConstantExpr.True(path.Node)
                    }
                );
                // index of while == whileIdx + 1
                var breaks = whileExpr.Scope.FindItemsOfType<BreakExpr>();
                var boolSetter = new BinaryExpr(path.Node) {
                    Left = flagName,
                    Operator = new OperatorToken(
                        path.Node.Unit,
                        tokenType: EqualsSign
                    ),
                    Right = ConstantExpr.True(path.Node)
                };
                foreach (var (_, parentScope, itemIndex) in breaks) {
                    parentScope.Items.Insert(itemIndex, boolSetter);
                }

                scope.Items.Insert(
                    whileIndex + 2,
                    new IfExpr(path.Node) {
                        Condition = flagName,
                        ThenScope = whileExpr.NoBreakScope
                    }
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
                foreach (var dataMember in cls.DataMembers) {
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
