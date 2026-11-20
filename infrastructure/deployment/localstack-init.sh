#!/bin/bash
# Recursos AWS emulados por LocalStack (equivalentes a infrastructure/aws/flowdesk.yaml).
set -e
awslocal s3 mb s3://flowdesk-documents
awslocal sqs create-queue --queue-name flowdesk-events-dlq
DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/flowdesk-events-dlq \
  --attribute-names QueueArn --query 'Attributes.QueueArn' --output text)
awslocal sqs create-queue --queue-name flowdesk-events \
  --attributes "{\"VisibilityTimeout\":\"60\",\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$DLQ_ARN\\\",\\\"maxReceiveCount\\\":\\\"5\\\"}\"}"
echo "LocalStack: bucket y colas creados."
