﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SuperSocket.Common;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Protocol;

namespace SuperSocket.SocketBase.Protocol
{
    /// <summary>
    /// FixedHeaderRequestFilter,
    /// it is the request filter base for the protocol which define fixed length header and the header contains the request body length,
    /// you can implement your own request filter for this kind protocol easily by inheriting this class 
    /// </summary>
    /// <typeparam name="TRequestInfo">The type of the request info.</typeparam>
    public abstract class FixedHeaderRequestFilter<TRequestInfo> : FixedSizeRequestFilter<TRequestInfo>
        where TRequestInfo : IRequestInfo
    {
        private bool m_FoundHeader = false;

        private ArraySegment<byte> m_Header;

        private int m_BodyLength;

        private ArraySegmentList m_BodyBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedHeaderRequestFilter&lt;TRequestInfo&gt;"/> class.
        /// </summary>
        /// <param name="headerSize">Size of the header.</param>
        public FixedHeaderRequestFilter(int headerSize)
            : base(headerSize)
        {

        }

        /// <summary>
        /// Filters the specified session.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="readBuffer">The read buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="toBeCopied">if set to <c>true</c> [to be copied].</param>
        /// <param name="left">The left.</param>
        /// <returns></returns>
        public override TRequestInfo Filter(IAppSession session, byte[] readBuffer, int offset, int length, bool toBeCopied, out int left)
        {
            if (!m_FoundHeader)
                return base.Filter(session, readBuffer, offset, length, toBeCopied, out left);

            if (m_BodyBuffer == null || m_BodyBuffer.Count == 0)
            {
                if (length < m_BodyLength)
                {
                    if (m_BodyBuffer == null)
                        m_BodyBuffer = new ArraySegmentList();

                    m_BodyBuffer.AddSegment(readBuffer, offset, length, toBeCopied);
                    left = 0;
                    return NullRequestInfo;
                }
                else if (length == m_BodyLength)
                {
                    left = 0;
                    m_FoundHeader = false;
                    return ResolveRequestInfo(m_Header, readBuffer, offset, length);
                }
                else
                {
                    left = length - m_BodyLength;
                    m_FoundHeader = false;
                    return ResolveRequestInfo(m_Header, readBuffer, offset, m_BodyLength);
                }
            }
            else
            {
                int required = m_BodyLength - m_BodyBuffer.Count;

                if (length < required)
                {
                    m_BodyBuffer.AddSegment(readBuffer, offset, length, toBeCopied);
                    left = 0;
                    return NullRequestInfo;
                }
                else if (length == required)
                {
                    m_BodyBuffer.AddSegment(readBuffer, offset, length, toBeCopied);
                    left = 0;
                    m_FoundHeader = false;
                    var requestInfo = ResolveRequestInfo(m_Header, m_BodyBuffer.ToArrayData());
                    m_BodyBuffer.ClearSegements();
                    return requestInfo;
                }
                else
                {
                    m_BodyBuffer.AddSegment(readBuffer, offset, required, toBeCopied);
                    left = length - required;
                    m_FoundHeader = false;
                    var requestInfo = ResolveRequestInfo(m_Header, m_BodyBuffer.ToArrayData(0, m_BodyLength));
                    m_BodyBuffer.ClearSegements();
                    return requestInfo;
                }
            }
        }

        /// <summary>
        /// Processes the fix size request.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="toBeCopied">if set to <c>true</c> [to be copied].</param>
        /// <param name="left">The left.</param>
        /// <returns></returns>
        protected override TRequestInfo ProcessMatchedRequest(IAppSession session, byte[] buffer, int offset, int length, bool toBeCopied, out int left)
        {
            m_FoundHeader = true;

            left = length - this.Size;

            m_BodyLength = GetBodyLengthFromHeader(buffer, offset, Size);

            if (toBeCopied)
                m_Header = new ArraySegment<byte>(buffer.CloneRange(offset, Size));
            else
                m_Header = new ArraySegment<byte>(buffer, offset, Size);

            return NullRequestInfo;
        }

        private TRequestInfo ResolveRequestInfo(ArraySegment<byte> header, byte[] bodyBuffer)
        {
            return ResolveRequestInfo(header, bodyBuffer, 0, bodyBuffer.Length);
        }

        /// <summary>
        /// Gets the body length from header.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns></returns>
        protected abstract int GetBodyLengthFromHeader(byte[] header, int offset, int length);

        /// <summary>
        /// Resolves the request data.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <param name="bodyBuffer">The body buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns></returns>
        protected abstract TRequestInfo ResolveRequestInfo(ArraySegment<byte> header, byte[] bodyBuffer, int offset, int length);

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_FoundHeader = false;
            m_BodyLength = 0;
        }
    }
}
