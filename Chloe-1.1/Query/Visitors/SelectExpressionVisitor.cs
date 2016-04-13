﻿using System;
using System.Linq.Expressions;
using System.Reflection;
using Chloe.Extensions;
using Chloe.Utility;
using Chloe.DbExpressions;
using Chloe.Query.Visitors;

namespace Chloe.Query
{
    class SelectExpressionVisitor : ExpressionVisitor<IMappingObjectExpression>
    {
        ExpressionVisitorBase _visitor;
        IMappingObjectExpression _moe;
        public SelectExpressionVisitor(ExpressionVisitorBase visitor, IMappingObjectExpression moe)
        {
            this._visitor = visitor;
            this._moe = moe;
        }

        DbExpression VisistExpression(Expression exp)
        {
            return this._visitor.Visit(exp);
        }
        IMappingObjectExpression VisitNavigationMember(MemberExpression exp)
        {
            return this._moe.GetNavMemberExpression(exp);
        }

        protected override IMappingObjectExpression VisitLambda(LambdaExpression exp)
        {
            return this.Visit(exp.Body);
        }

        protected override IMappingObjectExpression VisitNew(NewExpression exp)
        {
            IMappingObjectExpression result = new MappingObjectExpression(exp.Constructor);
            ParameterInfo[] parames = exp.Constructor.GetParameters();
            for (int i = 0; i < parames.Length; i++)
            {
                ParameterInfo pi = parames[i];
                Expression argExp = exp.Arguments[i];
                if (Utils.IsMapType(pi.ParameterType))
                {
                    DbExpression dbExpression = this.VisistExpression(argExp);
                    result.AddConstructorParameter(pi, dbExpression);
                }
                else
                {
                    IMappingObjectExpression subResult = this.Visit(argExp);
                    result.AddConstructorEntityParameter(pi, subResult);
                }
            }

            return result;
        }
        protected override IMappingObjectExpression VisitMemberInit(MemberInitExpression exp)
        {
            IMappingObjectExpression result = this.Visit(exp.NewExpression);

            foreach (MemberBinding binding in exp.Bindings)
            {
                if (binding.BindingType != MemberBindingType.Assignment)
                {
                    throw new NotSupportedException();
                }

                MemberAssignment memberAssignment = (MemberAssignment)binding;
                MemberInfo member = memberAssignment.Member;
                Type memberType = member.GetPropertyOrFieldType();

                //是数据库映射类型
                if (Utils.IsMapType(memberType))
                {
                    DbExpression dbExpression = this.VisistExpression(memberAssignment.Expression);
                    result.AddMemberExpression(member, dbExpression);
                }
                else
                {
                    //对于非数据库映射类型，只支持 NewExpression 和 MemberInitExpression
                    IMappingObjectExpression subResult = this.Visit(memberAssignment.Expression);
                    result.AddNavMemberExpression(member, subResult);
                }
            }

            return result;
        }
        /// <summary>
        /// a => a.Id   a => a.Name   a => a.User
        /// </summary>
        /// <param name="exp"></param>
        /// <returns></returns>
        protected override IMappingObjectExpression VisitMemberAccess(MemberExpression exp)
        {
            //create MappingFieldExpression object if exp is map type
            if (Utils.IsMapType(exp.Type))
            {
                DbExpression dbExp = this.VisistExpression(exp);
                MappingFieldExpression ret = new MappingFieldExpression(exp.Type, dbExp);
                return ret;
            }

            //如 a.Order a.User 等形式
            return this.VisitNavigationMember(exp);
        }
        protected override IMappingObjectExpression VisitParameter(ParameterExpression exp)
        {
            return this._moe;
        }
        protected override IMappingObjectExpression VisitConstant(ConstantExpression exp)
        {
            if (Utils.IsMapType(exp.Type))
            {
                DbExpression dbExp = this.VisistExpression(exp);
                MappingFieldExpression ret = new MappingFieldExpression(exp.Type, dbExp);
                return ret;
            }

            throw new NotSupportedException(exp.ToString());
        }
    }
}
